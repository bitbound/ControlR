using System.Net.WebSockets;
using Bitbound.SimpleMessenger;
using ControlR.Libraries.Clients.Services;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Streamer.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Streamer.Services;

public interface IStreamerStreamingClient : IHostedService
{
  Task SendCurrentClipboardText();
}

internal sealed class StreamerStreamingClient(
  IMessenger messenger,
  IHostApplicationLifetime appLifetime,
  IToaster toaster,
  IDesktopCapturer displayManager,
  IClipboardManager clipboardManager,
  IMemoryProvider memoryProvider,
  IInputSimulator inputSimulator,
  IOptions<StartupOptions> startupOptions,
  ILogger<StreamerStreamingClient> logger)
  : StreamingClient(messenger, memoryProvider, logger), IStreamerStreamingClient
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IClipboardManager _clipboardManager = clipboardManager;
  private readonly IDesktopCapturer _displayManager = displayManager;
  private readonly IInputSimulator _inputSimulator = inputSimulator;
  private readonly ILogger<StreamerStreamingClient> _logger = logger;
  private readonly IOptions<StartupOptions> _startupOptions = startupOptions;
  private readonly IToaster _toaster = toaster;

  private IDisposable? _messageHandlerRegistration;

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
      _messageHandlerRegistration = RegisterMessageHandler(this, HandleMessageReceived);

      await SendDisplayData();

      if (_startupOptions.Value.NotifyUser)
      {
        var message = _startupOptions.Value.ViewerName is { Length: > 0 } viewerName
          ? $"{viewerName} has joined your session"
          : "Remote control session has started";

        await _toaster.ShowToast("ControlR", message, BalloonTipIcon.Info);
      }

      StreamScreenToViewer().Forget();
    }
    catch (Exception ex)
    {
      _logger.LogError(
        ex,
        "Error while initializing streaming session. " +
        "Streaming cannot start.  Shutting down.");
      _appLifetime.StopApplication();
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    await DisposeAsync();
    _messageHandlerRegistration?.Dispose();
  }

  private async Task HandleCursorChangedMessage(object subscriber, CursorChangedMessage message)
  {
    try
    {
      var dto = new CursorChangedDto(message.Cursor, _startupOptions.Value.SessionId);
      var wrapper = DtoWrapper.Create(dto, DtoType.CursorChanged);
      await Send(wrapper, _appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling cursor change.");
    }
  }

  private async Task HandleDisplaySettingsChanged(object subscriber, DisplaySettingsChangedMessage message)
  {
    _displayManager.ResetDisplays();
    await SendDisplayData();
  }

  private async Task HandleMessageReceived(DtoWrapper wrapper)
  {
    try
    {
      using var logScope = _logger.BeginMemberScope();

      switch (wrapper.DtoType)
      {
        case DtoType.CloseStreamingSession:
        {
          _logger.LogInformation("Received request to close streaming session.");
          _appLifetime.StopApplication();
          break;
        }
        case DtoType.ChangeDisplays:
        {
          var payload = wrapper.GetPayload<ChangeDisplaysDto>();
          await _displayManager.ChangeDisplays(payload.DisplayId);
          break;
        }
        case DtoType.WheelScroll:
        {
          var payload = wrapper.GetPayload<WheelScrollDto>();
          var point = await _displayManager.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
          _inputSimulator.ScrollWheel(point.X, point.Y, (int)payload.ScrollY, (int)payload.ScrollX);
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
          var point = await _displayManager.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
          _inputSimulator.MovePointer(point.X, point.Y, MovePointerType.Absolute);
          break;
        }
        case DtoType.MouseButtonEvent:
        {
          var payload = wrapper.GetPayload<MouseButtonEventDto>();
          var point = await _displayManager.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
          _inputSimulator.MovePointer(point.X, point.Y, MovePointerType.Absolute);
          _inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, payload.IsPressed);
          break;
        }
        case DtoType.MouseClick:
        {
          var payload = wrapper.GetPayload<MouseClickDto>();
          var point = await _displayManager.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
          _inputSimulator.MovePointer(point.X, point.Y, MovePointerType.Absolute);
          _inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, true);
          _inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, false);

          if (payload.IsDoubleClick)
          {
            _inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, true);
            _inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, false);
          }

          break;
        }
        default:
          _logger.LogWarning("Unhandled DTO type: {type}", wrapper.DtoType);
          break;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling signed DTO.");
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
      var displays = _displayManager.GetDisplays().ToArray();
      var dto = new DisplayDataDto(
        _startupOptions.Value.SessionId,
        displays);

      var wrapper = DtoWrapper.Create(dto, DtoType.DisplayData);
      await Send(wrapper, _appLifetime.ApplicationStopping);
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

  private async Task StreamScreenToViewer()
  {
    await _displayManager.StartCapturingChanges();

    while (State == WebSocketState.Open && !_appLifetime.ApplicationStopping.IsCancellationRequested)
    {
      try
      {
        await foreach (var region in _displayManager.GetChangedRegions())
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

    _logger.LogInformation("Streaming session ended.  Shutting down.");
    _appLifetime.StopApplication();
  }
}