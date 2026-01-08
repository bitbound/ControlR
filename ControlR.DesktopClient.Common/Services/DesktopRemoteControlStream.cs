using System.Drawing;
using System.Net.WebSockets;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.WebSocketRelay.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Common.Services;

public interface IDesktopRemoteControlStream : IAsyncDisposable
{
  Task SendCurrentClipboardText();
  Task StreamScreen(CancellationToken cancellationToken);
}

internal sealed class DesktopRemoteControlStream(
  TimeProvider timeProvider,
  IMessenger messenger,
  ICaptureMetrics captureMetrics,
  IHostApplicationLifetime appLifetime,
  IToaster toaster,
  ISessionConsentService sessionConsentService,
  IDesktopCapturerFactory desktopCapturerFactory,
  IClipboardManager clipboardManager,
  IMemoryProvider memoryProvider,
  IInputSimulator inputSimulator,
  IDisplayManager displayManager,
  IWaiter waiter,
  ISystemEnvironment systemEnvironment,
  IOptions<RemoteControlSessionOptions> startupOptions,
  ILogger<DesktopRemoteControlStream> logger)
  : ManagedRelayStream(timeProvider, messenger, memoryProvider, waiter, logger), IDesktopRemoteControlStream
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly ICaptureMetrics _captureMetrics = captureMetrics;
  private readonly IClipboardManager _clipboardManager = clipboardManager;
  private readonly IDesktopCapturer _desktopCapturer = desktopCapturerFactory.GetOrCreate();
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly IInputSimulator _inputSimulator = inputSimulator;
  private readonly ILogger<DesktopRemoteControlStream> _logger = logger;
  private readonly TimeSpan _metricsWindow = TimeSpan.FromSeconds(3);
  private readonly ISessionConsentService _sessionConsentService = sessionConsentService;
  private readonly IOptions<RemoteControlSessionOptions> _startupOptions = startupOptions;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly IToaster _toaster = toaster;

  private IDisposable? _messageHandlerRegistration;

  public async ValueTask DisposeAsync()
  {
    await Close();
    _messageHandlerRegistration?.Dispose();
    await _desktopCapturer.DisposeAsync();
  }

  public async Task SendCurrentClipboardText()
  {
    try
    {
      var clipboardText = await _clipboardManager.GetText();
      var dto = new ClipboardTextDto(clipboardText, _startupOptions.Value.SessionId);
      var wrapper = DtoWrapper.Create(dto, DtoType.ClipboardText);
      await Send(wrapper, _appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending clipboard text.");
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
      Messenger.Register<SendToastToViewerMessage>(this, HandleSendToastToViewerMessage);
      Messenger.Register<SendBlockInputResultMessage>(this, HandleSendBlockInputResultMessage);
      _messageHandlerRegistration = RegisterMessageHandler(this, HandleMessageReceived);

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

      var wrapper = DtoWrapper.Create(dto, DtoType.CursorChanged);
      await Send(wrapper, _appLifetime.ApplicationStopping);
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

  private async Task HandleMessageReceived(DtoWrapper wrapper)
  {
    try
    {
      using var logScope = _logger.BeginMemberScope();

      switch (wrapper.DtoType)
      {
        case DtoType.CloseRemoteControlSession:
          {
            _logger.LogInformation("Received request to close remote control session.");
            _appLifetime.StopApplication();
            break;
          }
        case DtoType.ChangeDisplays:
          {
            var payload = wrapper.GetPayload<ChangeDisplaysDto>();
            await _desktopCapturer.ChangeDisplays(payload.DisplayId);
            break;
          }
        case DtoType.WheelScroll:
          {
            var payload = wrapper.GetPayload<WheelScrollDto>();
            var coordinates = await TryGetPointerCoordinates(payload.PercentX, payload.PercentY);
            if (coordinates == null)
            {
              break;
            }
            await _inputSimulator.ScrollWheel(coordinates, (int)payload.ScrollY, (int)payload.ScrollX);
            break;
          }
        case DtoType.KeyEvent:
          {
            var payload = wrapper.GetPayload<KeyEventDto>();
            await _inputSimulator.InvokeKeyEvent(payload.Key, payload.Code, payload.IsPressed);
            break;
          }
        case DtoType.ResetKeyboardState:
          {
            await _inputSimulator.ResetKeyboardState();
            break;
          }
        case DtoType.ClipboardText:
          {
            _logger.LogInformation("Received clipboard text from viewer.");
            var payload = wrapper.GetPayload<ClipboardTextDto>();
            await _clipboardManager.SetText(payload.Text);
            break;
          }
        case DtoType.RequestClipboardText:
          {
            _logger.LogInformation("Received request for clipboard text.");
            await SendCurrentClipboardText();
            break;
          }
        case DtoType.TypeText:
          {
            var payload = wrapper.GetPayload<TypeTextDto>();
            await _inputSimulator.TypeText(payload.Text);
            break;
          }
        case DtoType.MovePointer:
          {
            var payload = wrapper.GetPayload<MovePointerDto>();
            var coordinates = await TryGetPointerCoordinates(payload.PercentX, payload.PercentY);
            if (coordinates == null)
            {
              break;
            }
            await _inputSimulator.MovePointer(coordinates, MovePointerType.Absolute);
            break;
          }
        case DtoType.MouseButtonEvent:
          {
            var payload = wrapper.GetPayload<MouseButtonEventDto>();
            var coordinates = await TryGetPointerCoordinates(payload.PercentX, payload.PercentY);
            if (coordinates == null)
            {
              break;
            }
            await _inputSimulator.MovePointer(coordinates, MovePointerType.Absolute);
            await _inputSimulator.InvokeMouseButtonEvent(coordinates, payload.Button, payload.IsPressed);
            break;
          }
        case DtoType.MouseClick:
          {
            var payload = wrapper.GetPayload<MouseClickDto>();
            var coordinates = await TryGetPointerCoordinates(payload.PercentX, payload.PercentY);
            if (coordinates == null)
            {
              break;
            }
            await _inputSimulator.MovePointer(coordinates, MovePointerType.Absolute);
            await _inputSimulator.InvokeMouseButtonEvent(coordinates, payload.Button, true);
            await _inputSimulator.InvokeMouseButtonEvent(coordinates, payload.Button, false);

            if (payload.IsDoubleClick)
            {
              await _inputSimulator.InvokeMouseButtonEvent(coordinates, payload.Button, true);
              await _inputSimulator.InvokeMouseButtonEvent(coordinates, payload.Button, false);
            }
            break;
          }
        case DtoType.RequestKeyFrame:
          {
            _logger.LogInformation("Received request for key frame.");
            await _desktopCapturer.RequestKeyFrame();
            break;
          }
        case DtoType.ToggleBlockInput:
          {
            if (!_systemEnvironment.IsWindows())
            {
              _logger.LogWarning("ToggleBlockInput is only supported on Windows. Ignoring request.");
              break;
            }

            var payload = wrapper.GetPayload<ToggleBlockInputDto>();
            _logger.LogInformation("Toggling block input to {isEnabled}.", payload.IsEnabled);
            await _inputSimulator.SetBlockInput(payload.IsEnabled);
            break;
          }
        case DtoType.TogglePrivacyScreen:
          {
            if (!_systemEnvironment.IsWindows())
            {
              _logger.LogWarning("TogglePrivacyScreen is only supported on Windows. Ignoring request.");
              break;
            }

            var payload = wrapper.GetPayload<TogglePrivacyScreenDto>();
            _logger.LogInformation("Toggling privacy screen to {isEnabled}.", payload.IsEnabled);
            var result = await _displayManager.SetPrivacyScreen(payload.IsEnabled);
            
            var resultDto = new PrivacyScreenResultDto(result.IsSuccess, _displayManager.IsPrivacyScreenEnabled);
            var resultWrapper = DtoWrapper.Create(resultDto, DtoType.PrivacyScreenResult);
            await Send(resultWrapper, _appLifetime.ApplicationStopping);
            break;
          }
        default:
          _logger.LogWarning("Unhandled DTO type: {type}", wrapper.DtoType);
          break;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling DTO.");
    }
  }

  private async Task HandleSendBlockInputResultMessage(object subscriber, SendBlockInputResultMessage message)
  {
    try
    {
      var dto = new BlockInputResultDto(message.IsSuccess, message.IsEnabled);
      var wrapper = DtoWrapper.Create(dto, DtoType.BlockInputResult);
      await Send(wrapper, _appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending block input result to viewer.");
    }
  }

  private async Task HandleSendToastToViewerMessage(object subscriber, SendToastToViewerMessage message)
  {
    try
    {
      var dto = new ToastNotificationDto(message.Message, message.Severity);

      var wrapper = DtoWrapper.Create(dto, DtoType.ToastNotification);
      await Send(wrapper, _appLifetime.ApplicationStopping);
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
      var wrapper = DtoWrapper.Create(dto, DtoType.WindowsSessionEnding);
      await Send(wrapper, _appLifetime.ApplicationStopping);
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
      var wrapper = DtoWrapper.Create(dto, DtoType.WindowsSessionSwitched);
      await Send(wrapper, _appLifetime.ApplicationStopping);
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
        Height = x.MonitorArea.Height,
        IsPrimary = x.IsPrimary,
        Width = x.MonitorArea.Width,
        Name = x.DisplayName,
        Top = x.MonitorArea.Top,
        Left = x.MonitorArea.Left,
        ScaleFactor = x.ScaleFactor,
      });
      var dto = new DisplayDataDto([.. displayDtos]);

      var wrapper = DtoWrapper.Create(dto, DtoType.DisplayData);
      await Send(wrapper, _appLifetime.ApplicationStopping);
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
            ExtraData: _captureMetrics.GetExtraMetricsData());

          var wrapper = DtoWrapper.Create(captureMetrics, DtoType.CaptureMetricsChanged);
          await Send(wrapper, cancellationToken);
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
          await Send(wrapper, _appLifetime.ApplicationStopping);
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

  private async Task<PointerCoordinates?> TryGetPointerCoordinates(double percentX, double percentY)
  {
    var selectResult = await _desktopCapturer.TryGetSelectedDisplay();
    if (!selectResult.IsSuccess)
    {
      _logger.LogWarning("Selected display is invalid. Unable to process viewer request.");
      return null;
    }

    var selectedDisplay = selectResult.Value;
    var point = await _displayManager.ConvertPercentageLocationToAbsolute(
        selectedDisplay.DeviceName,
        percentX,
        percentY);

    if (point.IsEmpty)
    {
      _logger.LogWarning("Unable to convert percentage location to absolute coordinates.");
      return null;
    }

    return new PointerCoordinates(percentX, percentY, point, selectedDisplay);
  }
}