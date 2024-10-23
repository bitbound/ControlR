using Bitbound.SimpleMessenger;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.SidecarDtos;
using Microsoft.Extensions.Hosting;

namespace ControlR.Streamer.Services;

internal class DtoHandler(
  IDisplayManager displayManager,
  IClipboardManager clipboardManager,
  IInputSimulator inputSimulator,
  IHostApplicationLifetime appLifetime,
  IMessenger messenger,
  ILogger<DtoHandler> logger) : IHostedService
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    messenger.Register<DtoReceivedMessage<DtoWrapper>>(this, HandleDtoReceivedMessage);
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    messenger.Unregister<DtoReceivedMessage<DtoWrapper>>(this);
    return Task.CompletedTask;
  }

  private async Task HandleDtoReceivedMessage(object subscriber, DtoReceivedMessage<DtoWrapper> message)
  {
    try
    {
      using var logScope = logger.BeginMemberScope();
      var wrapper = message.Dto;

      switch (wrapper.DtoType)
      {
        case DtoType.CloseStreamingSession:
        {
          var payload = wrapper.GetPayload<CloseStreamingSessionRequestDto>();
          payload.VerifyType(DtoType.CloseStreamingSession);
          logger.LogInformation("Received request to close streaming session.");
          appLifetime.StopApplication();
          break;
        }
        case DtoType.ChangeDisplays:
        {
          var payload = wrapper.GetPayload<ChangeDisplaysDto>();
          await displayManager.ChangeDisplays(payload.DisplayId);
          break;
        }
        case DtoType.WheelScroll:
        {
          var payload = wrapper.GetPayload<WheelScrollDto>();
          var point = await displayManager.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
          inputSimulator.ScrollWheel(point.X, point.Y, (int)payload.ScrollY, (int)payload.ScrollX);
          break;
        }
        case DtoType.KeyEvent:
        {
          var payload = wrapper.GetPayload<KeyEventDto>();
          inputSimulator.InvokeKeyEvent(payload.Key, payload.IsPressed);
          break;
        }
        case DtoType.ResetKeyboardState:
        {
          var payload = wrapper.GetPayload<ResetKeyboardStateDto>();
          payload.VerifyType(DtoType.ResetKeyboardState);
          inputSimulator.ResetKeyboardState();
          break;
        }
        case DtoType.ClipboardChanged:
        {
          var payload = wrapper.GetPayload<ClipboardChangeDto>();
          await clipboardManager.SetText(payload.Text);
          break;
        }
        case DtoType.TypeText:
        {
          var payload = wrapper.GetPayload<TypeTextDto>();
          inputSimulator.TypeText(payload.Text);
          break;
        }
        case DtoType.MovePointer:
        {
          var payload = wrapper.GetPayload<MovePointerDto>();
          var point = await displayManager.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
          inputSimulator.MovePointer(point.X, point.Y, MovePointerType.Absolute);
          break;
        }
        case DtoType.MouseButtonEvent:
        {
          var payload = wrapper.GetPayload<MouseButtonEventDto>();
          var point = await displayManager.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
          inputSimulator.MovePointer(point.X, point.Y, MovePointerType.Absolute);
          inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, payload.IsPressed);
          break;
        }
        case DtoType.MouseClick:
        {
          var payload = wrapper.GetPayload<MouseClickDto>();
          var point = await displayManager.ConvertPercentageLocationToAbsolute(payload.PercentX, payload.PercentY);
          inputSimulator.MovePointer(point.X, point.Y, MovePointerType.Absolute);
          inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, true);
          inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, false);

          if (payload.IsDoubleClick)
          {
            inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, true);
            inputSimulator.InvokeMouseButtonEvent(point.X, point.Y, payload.Button, false);
          }

          break;
        }
        default:
          logger.LogWarning("Unhandled DTO type: {type}", wrapper.DtoType);
          break;
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while handling signed DTO.");
    }
  }
}