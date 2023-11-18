using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using ControlR.Devices.Common.Messages;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Services;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Agent.Services;

internal class DtoHandler(
    IKeyProvider _keyProvider,
    IAgentHubConnection _agentHub,
    IMessenger _messenger,
    IPowerControl _powerControl,
    ITerminalStore _terminalStore,
    ISettingsProvider _settings,
    ILogger<DtoHandler> _logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messenger.Register<SignedDtoReceivedMessage>(this, HandleSignedDtoReceivedMessage);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _messenger.Unregister<SignedDtoReceivedMessage>(this);
        return Task.CompletedTask;
    }

    private async Task HandleSignedDtoReceivedMessage(SignedDtoReceivedMessage message)
    {
        using var logScope = _logger.BeginMemberScope();
        var dto = message.SignedDto;

        if (!_keyProvider.Verify(dto))
        {
            _logger.LogCritical("Key verification failed for public key: {key}", dto.PublicKey);
            return;
        }

        if (!_settings.AuthorizedKeys.Contains(dto.PublicKeyBase64))
        {
            _logger.LogCritical("Public key does not exist in authorized keys: {key}", dto.PublicKey);
            return;
        }

        switch (dto.DtoType)
        {
            case DtoType.DeviceUpdateRequest:
                {
                    await _agentHub.SendDeviceHeartbeat();
                    break;
                }

            case DtoType.PowerStateChange:
                {
                    var powerDto = MessagePackSerializer.Deserialize<PowerStateChangeDto>(dto.Payload);
                    await _powerControl.ChangeState(powerDto.Type);
                    break;
                }

            case DtoType.CloseTerminalRequest:
                {
                    var closeSessionRequest = MessagePackSerializer.Deserialize<CloseTerminalRequest>(dto.Payload);
                    // Underyling process is killed/disposed upon eviction from the MemoryCache.
                    _ = _terminalStore.TryRemove(closeSessionRequest.TerminalId, out _);
                    break;
                }

            default:
                _logger.LogWarning("Unhandled DTO type: {type}", dto.DtoType);
                break;
        }
    }
}