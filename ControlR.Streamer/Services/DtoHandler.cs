using Bitbound.SimpleMessenger;
using Microsoft.Extensions.Hosting;

namespace ControlR.Streamer.Services;

internal class DtoHandler(
  IDesktopCapturer desktopCapturer,
  IClipboardManager clipboardManager,
  IInputSimulator inputSimulator,
  IHostApplicationLifetime appLifetime,
  IMessenger messenger,
  IStreamerStreamingClient streamingClient,
  ILogger<DtoHandler> logger) : IHostedService
{
  private readonly IDesktopCapturer _desktopCapturer = desktopCapturer;
  private readonly IClipboardManager _clipboardManager = clipboardManager;
  private readonly IInputSimulator _inputSimulator = inputSimulator;
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IMessenger _messenger = messenger;
  private readonly IStreamerStreamingClient _streamingClient = streamingClient;
  private readonly ILogger<DtoHandler> _logger = logger;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _messenger.Register<DtoReceivedMessage<DtoWrapper>>(this, HandleDtoReceivedMessage);
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _messenger.Unregister<DtoReceivedMessage<DtoWrapper>>(this);
    return Task.CompletedTask;
  }

  private async Task HandleDtoReceivedMessage(object subscriber, DtoReceivedMessage<DtoWrapper> message)
  {
    try
    {
      using var logScope = _logger.BeginMemberScope();
      var wrapper = message.Dto;

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
            await _desktopCapturer.ChangeDisplays(payload.DisplayId);
            break;
          }
        case DtoType.WheelScroll:
          {
            var payload = wrapper.GetPayload<WheelScrollDto>();
            var point = await _desktopCapturer.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
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
            await _streamingClient.SendCurrentClipboardText();
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
            var point = await _desktopCapturer.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
            _inputSimulator.MovePointer(point.X, point.Y, MovePointerType.Absolute);
            break;
          }
        case DtoType.MouseButtonEvent:
          {
            var payload = wrapper.GetPayload<MouseButtonEventDto>();
            var point = await _desktopCapturer.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
            _inputSimulator.MovePointer(point.X, point.Y, MovePointerType.Absolute);
            _inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, payload.IsPressed);
            break;
          }
        case DtoType.MouseClick:
          {
            var payload = wrapper.GetPayload<MouseClickDto>();
            var point = await _desktopCapturer.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
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
      _logger.LogError(ex, "Error while handling DTO.");
    }
  }
}