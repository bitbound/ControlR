using ControlR.Agent.Common.Interfaces;
using ControlR.Devices.Native.Services;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

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
  private readonly IAgentHubConnection _agentHub = agentHub;
  private readonly IAgentUpdater _agentUpdater = agentUpdater;
  private readonly ILogger<DtoHandler> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly IPowerControl _powerControl = powerControl;
  private readonly ITerminalStore _terminalStore = terminalStore;
  private readonly IWakeOnLanService _wakeOnLan = wakeOnLan;
  private readonly IWin32Interop _win32Interop = win32Interop;

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
      _logger.LogInformation("Received DTO of type {DtoType}", wrapper.DtoType);

      switch (wrapper.DtoType)
      {
        case DtoType.DeviceUpdateRequest:
          {
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
            // Underlying process is killed/disposed upon eviction from the MemoryCache.
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
        case DtoType.RefreshDeviceInfoRequest:
          {
            await _agentHub.SendDeviceHeartbeat();
            break;
          }
        default:
          _logger.LogWarning("Unhandled DTO type: {DtoType}", wrapper.DtoType);
          break;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling signed DTO.");
    }
  }
}