﻿using Bitbound.SimpleMessenger;
using ControlR.Libraries.DevicesCommon.Messages;
using ControlR.Libraries.Shared.Dtos.SidecarDtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Streamer.Services;
internal class DtoHandler(
    IKeyProvider _keyProvider,
    IDisplayManager _displayManager,
    IClipboardManager _clipboardManager,
    IInputSimulator _inputSimulator,
    IHostApplicationLifetime _appLifetime,
    IMessenger _messenger,
    IOptions<StartupOptions> _startupOptions,
    ILogger<DtoHandler> _logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messenger.Register<DtoReceivedMessage<SignedPayloadDto>>(this, HandleSignedDtoReceivedMessage);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _messenger.Unregister<DtoReceivedMessage<SignedPayloadDto>>(this);
        return Task.CompletedTask;
    }

    private async Task HandleSignedDtoReceivedMessage(object subscriber, DtoReceivedMessage<SignedPayloadDto> message)
    {
        try
        {
            using var logScope = _logger.BeginMemberScope();
            var wrapper = message.Dto;

            if (!_keyProvider.Verify(wrapper))
            {
                _logger.LogCritical("Key verification failed for public key: {PublicKey}", wrapper.PublicKeyBase64);
                return;
            }

            if (_startupOptions.Value.AuthorizedKey != wrapper.PublicKeyBase64)
            {
                _logger.LogCritical("Public key does not exist in authorized keys: {PublicKey}", wrapper.PublicKeyBase64);
                return;
            }

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