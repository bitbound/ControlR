using System.Net.WebSockets;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.WebSocketRelay.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Common.Services;

public interface IDesktopRemoteControlStream : IManagedRelayStream
{
  Task RequestDisconnect();
  Task StreamScreen(CancellationToken cancellationToken);
}

internal sealed class DesktopRemoteControlStream(
  TimeProvider timeProvider,
  IMessenger messenger,
  IStreamMetrics streamMetrics,
  ICaptureMetrics captureMetrics,
  IHostApplicationLifetime appLifetime,
  IToaster toaster,
  ISessionConsentService sessionConsentService,
  IDesktopCapturerFactory desktopCapturerFactory,
  IMemoryProvider memoryProvider,
  IDisplayManager displayManager,
  IIpcClientAccessor ipcClientAccessor,
  IRemoteControlDtoHandler dtoHandler,
  IWaiter waiter,
  IOptions<RemoteControlSessionOptions> startupOptions,
  ILogger<DesktopRemoteControlStream> logger)
  : ManagedRelayStream(timeProvider, messenger, memoryProvider, waiter, streamMetrics, logger), IDesktopRemoteControlStream
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly ICaptureMetrics _captureMetrics = captureMetrics;
  private readonly IDesktopCapturer _desktopCapturer = desktopCapturerFactory.GetOrCreate();
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly IRemoteControlDtoHandler _dtoHandler = dtoHandler;
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly ILogger<DesktopRemoteControlStream> _logger = logger;
  private readonly TimeSpan _metricsWindow = TimeSpan.FromSeconds(3);
  private readonly ISessionConsentService _sessionConsentService = sessionConsentService;
  private readonly IOptions<RemoteControlSessionOptions> _startupOptions = startupOptions;
  private readonly IToaster _toaster = toaster;

  /// <summary>
  /// Relay DTO handling, which a viewer older than v0.30.x still relies on for every DTO kind.
  /// </summary>
  private IDisposable? _relayDtoRegistration;

  public async Task RequestDisconnect()
  {
    try
    {
      var dto = new SessionDisconnectRequestedDto();
      var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.SessionDisconnectRequested);
      if (!await SendDto(wrapper, _appLifetime.ApplicationStopping).ConfigureAwait(false))
      {
        await Close();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while requesting disconnect.");
    }
  }

  public async Task StreamScreen(CancellationToken cancellationToken)
  {
    var viewerName = _startupOptions.Value.ViewerName is { Length: > 0 } vn
        ? vn
        : Localization.ADeviceAdministrator;

    try
    {

      if (_startupOptions.Value.RequireConsent)
      {
        var consentGranted = await _sessionConsentService.RequestConsentAsync(viewerName, cancellationToken);
        if (!consentGranted)
        {
          _logger.LogWarning("User denied consent for remote control session. Shutting down.");
          _appLifetime.StopApplication();
          return;
        }
      }

      await Connect(_startupOptions.Value.WebSocketUri, _appLifetime.ApplicationStopping);
      Messenger.Register<DisplaySettingsChangedMessage>(this, HandleDisplaySettingsChanged);
      Messenger.Register<WindowsSessionEndingMessage>(this, HandleWindowsSessionEndingMessage);
      Messenger.Register<WindowsSessionSwitchedMessage>(this, HandleWindowsSessionSwitchedMessage);
      Messenger.Register<CursorChangedMessage>(this, HandleCursorChangedMessage);
      Messenger.Register<SendRemoteControlDtoToViewerMessage>(this, HandleSendDtoToViewerMessage);
      Messenger.Register<SendToastToViewerMessage>(this, HandleSendToastToViewerMessage);
      OnClosed(HandleConnectionClosed);

      // A viewer that can receive commands over the hub gets them there, where the server authorizes
      // each one.  Leaving this handler registered for such a session would let anything the relay
      // delivers run unauthenticated, since the relay carries no permission check.
      if (!_startupOptions.Value.ViewerSupportsRemoteControlDtoHub)
      {
        _relayDtoRegistration = RegisterMessageHandler(this, HandleRelayDto);
      }

      await _displayManager.ReloadDisplays();

      await SendDisplayData();

      if (_startupOptions.Value.NotifyUser)
      {
        var message = string.Format(Localization.RemoteControlSessionStartToastMessage, viewerName);
        await _toaster.ShowToast(Localization.RemoteControlSessionToastTitle, message, ToastIcon.Info);
      }

      using var metricsTimer = StartMetricsTimer(_appLifetime.ApplicationStopping);
      await StreamScreenToViewer(_appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      _logger.LogError(
        ex,
        "Error while initializing remote control session. " +
        "Remote control cannot start.  Shutting down.");

      await TrySendSessionError(ex);
    }
    finally
    {
      if (_startupOptions.Value.NotifyUser)
      {
        var message = string.Format(Localization.RemoteControlSessionEndToastMessage, viewerName);
        await _toaster.ShowToast(Localization.RemoteControlSessionToastTitle, message, ToastIcon.Info);
      }
      _appLifetime.StopApplication();
    }
  }

  protected override async ValueTask DisposeAsync(bool disposing)
  {
    await base.DisposeAsync(disposing);
    if (disposing)
    {
      _relayDtoRegistration?.Dispose();
      await _desktopCapturer.DisposeAsync();
    }
  }

  private Task HandleConnectionClosed()
  {
    _appLifetime.StopApplication();
    return Task.CompletedTask;
  }

  private async Task HandleCursorChangedMessage(object subscriber, CursorChangedMessage message)
  {
    try
    {
      var dto = new CursorChangedDto(
        message.Cursor,
        message.CustomCursorBase64Png,
        message.XHotspot,
        message.YHotspot,
        _startupOptions.Value.SessionId);

      var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.CursorChanged);
      await SendDto(wrapper, _appLifetime.ApplicationStopping);
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Application shutting down.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling cursor change.");
    }
  }

  private async Task HandleDisplaySettingsChanged(object subscriber, DisplaySettingsChangedMessage message)
  {
    await _displayManager.ReloadDisplays();
    await SendDisplayData();
  }

  private async Task HandleRelayDto(RemoteControlDtoWrapper wrapper)
  {
    // Only reached for a viewer that predates the hub command path.  The relay has no return channel,
    // so such a viewer never learns whether its commands landed.
    await _dtoHandler.Handle(wrapper);
  }

  private async Task HandleSendDtoToViewerMessage(
    object subscriber,
    SendRemoteControlDtoToViewerMessage message)
  {
    await SendDto(message.Wrapper, _appLifetime.ApplicationStopping);
  }

  private async Task HandleSendToastToViewerMessage(object subscriber, SendToastToViewerMessage message)
  {
    try
    {
      var dto = new ToastNotificationDto(message.Message, message.Severity);

      var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.ToastNotification);
      await SendDto(wrapper, _appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending toast notification to viewer.");
    }
  }

  private async Task HandleWindowsSessionEndingMessage(object subscriber, WindowsSessionEndingMessage message)
  {
    try
    {
      var dto = new WindowsSessionEndingDto();
      var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.WindowsSessionEnding);
      await SendDto(wrapper, _appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling Windows session ending.");
    }
  }

  private async Task HandleWindowsSessionSwitchedMessage(object subscriber, WindowsSessionSwitchedMessage message)
  {
    try
    {
      var dto = new WindowsSessionSwitchedDto();
      var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.WindowsSessionSwitched);
      await SendDto(wrapper, _appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling Windows session switch.");
    }
  }

  private async Task SendDisplayData()
  {
    try
    {
      var displays = await _displayManager.GetDisplays();
      var displayDtos = displays.Select(x => new DisplayDto
      {
        DisplayId = x.DeviceName,
        CapturePixelSize = new PixelSizeDto
        {
          Width = x.CapturePixelSize.Width,
          Height = x.CapturePixelSize.Height,
        },
        IsPrimary = x.IsPrimary,
        Name = x.DisplayName,
        LayoutBounds = new DisplayBoundsDto
        {
          X = x.LayoutBounds.Left,
          Y = x.LayoutBounds.Top,
          Width = x.LayoutBounds.Width,
          Height = x.LayoutBounds.Height,
        },
        LayoutCoordinateSpace = (ControlR.Libraries.Api.Contracts.Enums.DisplayLayoutCoordinateSpace)x.LayoutCoordinateSpace,
        NativePixelSize = x.NativePixelSize is { } nativePixelSize
          ? new PixelSizeDto
          {
            Width = nativePixelSize.Width,
            Height = nativePixelSize.Height,
          }
          : null,
        Index = x.Index
      });
      var dto = new DisplayDataDto([.. displayDtos]);

      var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.DisplayData);
      await SendDto(wrapper, _appLifetime.ApplicationStopping);
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Application shutting down.");
    }
    catch (Exception ex)
    {
      _logger.LogError(
        ex,
        "Error while sending streamer init data. " +
        "Streaming cannot start.  Shutting down.");
      _appLifetime.StopApplication();
    }
  }

  private async Task<bool> SendDto(RemoteControlDtoWrapper wrapper, CancellationToken cancellationToken)
  {
    if (wrapper.DtoType.IsScreenFrame())
    {
      await Send(wrapper, cancellationToken);
      return true;
    }

    if (!_startupOptions.Value.ViewerSupportsRemoteControlDtoHub)
    {
      await Send(wrapper, cancellationToken);
      return true;
    }

    if (!_ipcClientAccessor.TryGetClient(out var client))
    {
      _logger.LogWarning("IPC client is not connected.  Dropping {DtoType} DTO.", wrapper.DtoType);
      return false;
    }

    if (string.IsNullOrWhiteSpace(_startupOptions.Value.ViewerConnectionId))
    {
      _logger.LogWarning("Viewer connection ID is missing.  Dropping {DtoType} DTO.", wrapper.DtoType);
      return false;
    }

    await client.Server.SendRemoteControlDto(_startupOptions.Value.ViewerConnectionId, wrapper);
    return true;
  }

  private Timer StartMetricsTimer(CancellationToken cancellationToken)
  {
    return new Timer(
      callback: async (state) =>
      {
        try
        {
          var captureMetrics = new CaptureMetricsDto(
            Fps: _desktopCapturer.GetCurrentFps(_metricsWindow),
            CaptureMode: _desktopCapturer.GetCaptureMode(),
            CurrentQuality: _desktopCapturer.GetCurrentQuality(),
            ExtraData: _captureMetrics.GetExtraMetricsData());

          var wrapper = RemoteControlDtoWrapper.Create(captureMetrics, RemoteControlDtoType.CaptureMetricsChanged);
          await SendDto(wrapper, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
          _logger.LogInformation(ex, "Application shutting down.");
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while handling capture metrics change.");
        }
      },
      state: null,
      dueTime: TimeSpan.FromSeconds(1),
      period: _metricsWindow);
  }

  private async Task StreamScreenToViewer(CancellationToken cancellationToken)
  {
    await _desktopCapturer.StartCapturingChanges(cancellationToken);
    while (State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
    {
      try
      {
        await foreach (var wrapper in _desktopCapturer.GetCaptureStream(cancellationToken))
        {
          await SendDto(wrapper, _appLifetime.ApplicationStopping);
        }
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while sending screen frame.");
        break;
      }
    }

    _logger.LogInformation("Remote control session ended.  Shutting down.");
    _appLifetime.StopApplication();
  }

  private async Task TrySendSessionError(Exception ex)
  {
    try
    {
      var message = ex.GetBaseException().Message;
      if (string.IsNullOrWhiteSpace(message))
      {
        message = "An error occurred while initializing remote control.";
      }

      var dto = new RemoteControlSessionErrorDto(message, IsFatal: true);
      var wrapper = RemoteControlDtoWrapper.Create(dto, RemoteControlDtoType.RemoteControlSessionError);

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
      await SendDto(wrapper, cts.Token);
    }
    catch (Exception sendEx)
    {
      _logger.LogError(sendEx, "Error while sending remote control session error to viewer.");
    }
  }
}