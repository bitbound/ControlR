using System.Drawing;
using System.Net.WebSockets;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.Libraries.Clients.Services;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Common.Services;

public interface IDesktopStreamingClient : IHostedService
{
  Task SendCurrentClipboardText();
}

internal sealed class DesktopStreamingClient(
  TimeProvider timeProvider,
  IMessenger messenger,
  IHostApplicationLifetime appLifetime,
  IToaster toaster,
  IDesktopCapturer desktopCapturer,
  IClipboardManager clipboardManager,
  IMemoryProvider memoryProvider,
  IInputSimulator inputSimulator,
  IDisplayManager displayManager,
  IWaiter waiter,
  IOptions<StreamingSessionOptions> startupOptions,
  ILogger<DesktopStreamingClient> logger)
  : StreamingClient(timeProvider, messenger, memoryProvider, waiter, logger), IDesktopStreamingClient
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IClipboardManager _clipboardManager = clipboardManager;
  private readonly IDesktopCapturer _desktopCapturer = desktopCapturer;
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly IInputSimulator _inputSimulator = inputSimulator;
  private readonly ILogger<DesktopStreamingClient> _logger = logger;
  private readonly IOptions<StreamingSessionOptions> _startupOptions = startupOptions;
  private readonly IToaster _toaster = toaster;

  private IDisposable? _messageHandlerRegistration;
  private Task? _streamTask;

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

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    try
    {
      await Connect(_startupOptions.Value.WebSocketUri, _appLifetime.ApplicationStopping);
      Messenger.Register<DisplaySettingsChangedMessage>(this, HandleDisplaySettingsChanged);
      Messenger.Register<WindowsSessionEndingMessage>(this, HandleWindowsSessionEndingMessage);
      Messenger.Register<WindowsSessionSwitchedMessage>(this, HandleWindowsSessionSwitchedMessage);
      Messenger.Register<CursorChangedMessage>(this, HandleCursorChangedMessage);
      Messenger.Register<CaptureMetricsChangedMessage>(this, HandleCaptureMetricsChanged);
      _messageHandlerRegistration = RegisterMessageHandler(this, HandleMessageReceived);

      await SendDisplayData();

      if (_startupOptions.Value.NotifyUser)
      {
        var viewerName = _startupOptions.Value.ViewerName is { Length: > 0 } vn
          ? vn
          : Localization.ADeviceAdministrator;

        var message = string.Format(Localization.RemoteControlSessionToastMessage, viewerName);
        await _toaster.ShowToast(Localization.RemoteControlSessionToastTitle, message, ToastIcon.Info);
      }

      _streamTask = StreamScreenToViewer(_appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      _logger.LogError(
        ex,
        "Error while initializing remote control session. " +
        "Remote control cannot start.  Shutting down.");
      _appLifetime.StopApplication();
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    await Close();
    _messageHandlerRegistration?.Dispose();
    if (_streamTask is not null)
    {
      await _streamTask.WaitAsync(cancellationToken);
    }
  }

  private async Task HandleCaptureMetricsChanged(object subscriber, CaptureMetricsChangedMessage message)
  {
    try
    {
      var metricsDto = message.MetricsDto with { Latency = CurrentLatency };
      var wrapper = DtoWrapper.Create(metricsDto, DtoType.CaptureMetricsChanged);
      await Send(wrapper, _appLifetime.ApplicationStopping);
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Application shutting down.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling capture metrics change.");
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
            var displayPoint = await TryGetSelectedDisplayPoint(payload.PercentX, payload.PercentY);
            if (displayPoint == null)
            {
              break;
            }
            _inputSimulator.ScrollWheel(displayPoint.Point.X, displayPoint.Point.Y, displayPoint.Display, (int)payload.ScrollY, (int)payload.ScrollX);
            break;
          }
        case DtoType.KeyEvent:
          {
            var payload = wrapper.GetPayload<KeyEventDto>();
            _inputSimulator.InvokeKeyEvent(payload.Key, payload.IsPressed);
            break;
          }
        case DtoType.ResetKeyboardState:
          {
            _inputSimulator.ResetKeyboardState();
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
            _inputSimulator.TypeText(payload.Text);
            break;
          }
        case DtoType.MovePointer:
          {
            var payload = wrapper.GetPayload<MovePointerDto>();
            var displayPoint = await TryGetSelectedDisplayPoint(payload.PercentX, payload.PercentY);
            if (displayPoint == null)
            {
              break;
            }
            _inputSimulator.MovePointer(displayPoint.Point.X, displayPoint.Point.Y, displayPoint.Display, MovePointerType.Absolute);
            break;
          }
        case DtoType.MouseButtonEvent:
          {
            var payload = wrapper.GetPayload<MouseButtonEventDto>();
            var displayPoint = await TryGetSelectedDisplayPoint(payload.PercentX, payload.PercentY);
            if (displayPoint == null)
            {
              break;
            }
            _inputSimulator.MovePointer(displayPoint.Point.X, displayPoint.Point.Y, displayPoint.Display, MovePointerType.Absolute);
            _inputSimulator.InvokeMouseButtonEvent(displayPoint.Point.X, displayPoint.Point.Y, displayPoint.Display, payload.Button, payload.IsPressed);
            break;
          }
        case DtoType.MouseClick:
          {
            var payload = wrapper.GetPayload<MouseClickDto>();
            var displayPoint = await TryGetSelectedDisplayPoint(payload.PercentX, payload.PercentY);
            if (displayPoint == null)
            {
              break;
            }
            _inputSimulator.MovePointer(displayPoint.Point.X, displayPoint.Point.Y, displayPoint.Display, MovePointerType.Absolute);
            _inputSimulator.InvokeMouseButtonEvent(displayPoint.Point.X, displayPoint.Point.Y, displayPoint.Display, payload.Button, true);
            _inputSimulator.InvokeMouseButtonEvent(displayPoint.Point.X, displayPoint.Point.Y, displayPoint.Display, payload.Button, false);

            if (payload.IsDoubleClick)
            {
              _inputSimulator.InvokeMouseButtonEvent(displayPoint.Point.X, displayPoint.Point.Y, displayPoint.Display, payload.Button, true);
              _inputSimulator.InvokeMouseButtonEvent(displayPoint.Point.X, displayPoint.Point.Y, displayPoint.Display, payload.Button, false);
            }
            break;
          }
        case DtoType.RequestKeyFrame:
          {
            _logger.LogInformation("Received request for key frame.");
            await _desktopCapturer.RequestKeyFrame();
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

  private async Task StreamScreenToViewer(CancellationToken cancellationToken)
  {
    await _desktopCapturer.StartCapturingChanges(cancellationToken);

    while (State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
    {
      try
      {
        await foreach (var region in _desktopCapturer.GetCaptureStream(cancellationToken))
        {
          var wrapper = DtoWrapper.Create(region, DtoType.ScreenRegion);
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

  private async Task<DisplayPointResult?> TryGetSelectedDisplayPoint(double percentX, double percentY)
  {
    if (!_desktopCapturer.TryGetSelectedDisplay(out var selectedDisplay))
    {
      _logger.LogWarning("Selected display is invalid. Unable to process viewer request.");
      return null;
    }

    var point = await _displayManager.ConvertPercentageLocationToAbsolute(
        selectedDisplay.DeviceName,
        percentX,
        percentY);

    if (point.IsEmpty)
    {
      _logger.LogWarning("Unable to convert percentage location to absolute coordinates.");
      return null;
    }

    return new DisplayPointResult(selectedDisplay, point);
  }

  private record DisplayPointResult(DisplayInfo Display, Point Point);
}