using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Clients.Messages;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Agent.Services;

internal class DtoHandler(
    IAgentHubConnection _agentHub,
    IMessenger _messenger,
    IPowerControl _powerControl,
    ITerminalStore _terminalStore,
    IWin32Interop _win32Interop,
    IWakeOnLanService _wakeOnLan,
    IAgentUpdater _agentUpdater,
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
                case DtoType.DeviceUpdateRequest:
                    {
                        var dto = wrapper.GetPayload<DeviceUpdateRequestDto>();
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