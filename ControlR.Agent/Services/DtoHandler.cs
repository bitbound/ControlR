using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Services;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Services;

internal class DtoHandler(
    IKeyProvider _keyProvider,
    IAgentHubConnection _agentHub,
    IPowerControl _powerControl,
    IOptionsMonitor<AppOptions> _appOptions,
    ILogger<DtoHandler> _logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _agentHub.DtoReceived += AgentHub_DtoReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _agentHub.DtoReceived -= AgentHub_DtoReceived;
        return Task.CompletedTask;
    }

    private async void AgentHub_DtoReceived(object? sender, SignedPayloadDto dto)
    {
        using var _ = _logger.BeginMemberScope();

        if (!_keyProvider.Verify(dto))
        {
            _logger.LogCritical("Key verification failed for public key: {key}", dto.PublicKey);
            return;
        }

        if (!_appOptions.CurrentValue.AuthorizedKeys.Contains(dto.PublicKeyBase64))
        {
            _logger.LogCritical("Public key does not exist in authorized keys: {key}", dto.PublicKey);
            return;
        }

        switch (dto.DtoType)
        {
            case DtoType.DeviceUpdateRequest:
                await _agentHub.SendDeviceHeartbeat();
                break;

            case DtoType.PowerStateChange:
                var powerDto = MessagePackSerializer.Deserialize<PowerStateChangeDto>(dto.Payload);
                await _powerControl.ChangeState(powerDto.Type);
                break;

            default:
                _logger.LogWarning("Unhandled DTO type: {type}", dto.DtoType);
                break;
        }
    }
}