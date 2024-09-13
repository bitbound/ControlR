using Bitbound.SimpleMessenger;
using ControlR.Libraries.Clients.Messages;
using ControlR.Libraries.Shared.Dtos.SidecarDtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Streamer.Services;
internal class DtoHandler(
    IDisplayManager _displayManager,
    IClipboardManager _clipboardManager,
    IInputSimulator _inputSimulator,
    IHostApplicationLifetime _appLifetime,
    IMessenger _messenger,
    ILogger<DtoHandler> _logger) : IHostedService
{
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
                        var payload = wrapper.GetPayload<CloseStreamingSessionRequestDto>();
                        payload.VerifyType(DtoType.CloseStreamingSession);
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
                        var payload = wrapper.GetPayload<ResetKeyboardStateDto>();
                        payload.VerifyType(DtoType.ResetKeyboardState);
                        _inputSimulator.ResetKeyboardState();
                        break;
                    }
                case DtoType.ClipboardChanged:
                    {
                        var payload = wrapper.GetPayload<ClipboardChangeDto>();
                        await _clipboardManager.SetText(payload.Text);
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
}