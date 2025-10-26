using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Hubs;

public interface IMainBrowserHub : IBrowserHubBase
{
  Task<Result<ServerStatsDto>> GetServerStats();
  Task InvokeCtrlAltDel(Guid deviceId);
  Task RefreshDeviceInfo(Guid deviceId);
  Task<Result> RequestVncSession(Guid deviceId, VncSessionRequestDto sessionRequestDto);
  Task SendAgentUpdateTrigger(Guid deviceId);
  Task SendPowerStateChange(Guid deviceId, PowerStateChangeType changeType);
  Task SendWakeDevice(Guid deviceId, string[] macAddresses);
  Task UninstallAgent(Guid deviceId, string reason);
}