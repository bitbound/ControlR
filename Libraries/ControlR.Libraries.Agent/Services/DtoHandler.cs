using ControlR.Devices.Native.Services;
using ControlR.Libraries.Agent.Interfaces;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Hosting;

namespace ControlR.Libraries.Agent.Services;

internal class DtoHandler(
  IAgentHubConnection agentHub,
  IMessenger messenger,
  IPowerControl powerControl,
  ITerminalStore terminalStore,
  IWin32Interop win32Interop,
  IWakeOnLanService wakeOnLan,
  IAgentUpdater agentUpdater,
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
        case DtoType.DeviceUpdateRequest:
          {
            var dto = wrapper.GetPayload<DeviceUpdateRequestDto>();
            await agentHub.SendDeviceHeartbeat();
            break;
          }

        case DtoType.PowerStateChange:
          {
            var powerDto = wrapper.GetPayload<PowerStateChangeDto>();
            await powerControl.ChangeState(powerDto.Type);
            break;
          }

        case DtoType.CloseTerminalRequest:
          {
            var closeSessionRequest = wrapper.GetPayload<CloseTerminalRequestDto>();
            // Underyling process is killed/disposed upon eviction from the MemoryCache.
            _ = terminalStore.TryRemove(closeSessionRequest.TerminalId, out _);
            break;
          }

        case DtoType.WakeDevice:
          {
            var wakeDto = wrapper.GetPayload<WakeDeviceDto>();
            await wakeOnLan.WakeDevices(wakeDto.MacAddresses);
            break;
          }

        case DtoType.InvokeCtrlAltDel:
          {
            var payload = wrapper.GetPayload<InvokeCtrlAltDelRequestDto>();
            payload.VerifyType(DtoType.InvokeCtrlAltDel);

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
              win32Interop.InvokeCtrlAltDel();
            }

            break;
          }
        case DtoType.TriggerAgentUpdate:
          {
            var payload = wrapper.GetPayload<TriggerAgentUpdateDto>();
            payload.VerifyType(DtoType.TriggerAgentUpdate);

            await agentUpdater.CheckForUpdate();
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