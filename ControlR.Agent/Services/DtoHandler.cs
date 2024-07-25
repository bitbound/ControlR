using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.DevicesCommon.Messages;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Services;
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
    IWin32Interop _win32Interop,
    IWakeOnLanService _wakeOnLan,
    IAgentUpdater _agentUpdater,
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
                _logger.LogCritical("Key verification failed for public key: {key}", wrapper.PublicKeyBase64);
                return;
            }

            if (!_settings.AuthorizedKeys.Any(x => x.PublicKey == wrapper.PublicKeyBase64))
            {
                _logger.LogCritical("Public key does not exist in authorized keys: {key}", wrapper.PublicKeyBase64);
                return;
            }

            switch (wrapper.DtoType)
            {
                case DtoType.DeviceUpdateRequest:
                    {
                        var dto = wrapper.GetPayload<DeviceUpdateRequestDto>();
                        await _settings.UpdatePublicKeyLabel(wrapper.PublicKeyBase64, dto.PublicKeyLabel);
                        await _agentHub.SendDeviceHeartbeat();
                        break;
                    }

                case DtoType.PowerStateChange:
                    {
                        var powerDto = wrapper.GetPayload<PowerStateChangeDto>();
                        await _powerControl.ChangeState(powerDto.Type);
                        break;
                    }

                case DtoType.CloseTerminalRequest:
                    {
                        var closeSessionRequest = wrapper.GetPayload<CloseTerminalRequestDto>();
                        // Underyling process is killed/disposed upon eviction from the MemoryCache.
                        _ = _terminalStore.TryRemove(closeSessionRequest.TerminalId, out _);
                        break;
                    }

                case DtoType.WakeDevice:
                    {
                        var wakeDto = wrapper.GetPayload<WakeDeviceDto>();
                        await _wakeOnLan.WakeDevices(wakeDto.MacAddresses);
                        break;
                    }

                case DtoType.InvokeCtrlAltDel:
                    {
                        var payload = wrapper.GetPayload<InvokeCtrlAltDelRequestDto>();
                        payload.VerifyType(DtoType.InvokeCtrlAltDel);

                        if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                        {
                            _win32Interop.InvokeCtrlAltDel();
                        }
                        break;
                    }
                case DtoType.TriggerAgentUpdate:
                    {
                        var payload = wrapper.GetPayload<TriggerAgentUpdateDto>();
                        payload.VerifyType(DtoType.TriggerAgentUpdate);

                        await _agentUpdater.CheckForUpdate();
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