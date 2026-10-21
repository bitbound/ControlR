// Deprecated hub methods kept so that a consumer shipping on its own schedule can still call them.
// Each one forwards to its numbered successor.  Nothing new belongs here.

using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.Api.Contracts.Hubs;

public partial interface IViewerHub
{
  [Obsolete("Use AddViewerActivity2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> AddViewerActivity(string activityName);

  [Obsolete("Use CloseChatSession2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> CloseChatSession(Guid deviceId, Guid sessionId, int targetProcessId);

  [Obsolete("Use CloseTerminalSession2. (deprecated 2026-09-03, v0.28.x)")]
  Task CloseTerminalSession(Guid deviceId, Guid terminalSessionId);

  [Obsolete("Use CreateTerminalSession2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> CreateTerminalSession(
    Guid deviceId,
    Guid terminalSessionId);

  [Obsolete("Use GetActiveDesktopSessions2. (deprecated 2026-09-03, v0.28.x)")]
  Task<DesktopSession[]> GetActiveDesktopSessions(Guid deviceId);

  [Obsolete("Use GetDeviceAccessPermissions2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult<DeviceAccessPermissionsDto>> GetDeviceAccessPermissions(Guid deviceId);

  [Obsolete("Use InvokeCtrlAltDel2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> InvokeCtrlAltDel(Guid deviceId, int targetDesktopProcessId, DesktopSessionType desktopSessionType);

  [Obsolete("Use RefreshDeviceInfo2. (deprecated 2026-09-03, v0.28.x)")]
  Task RefreshDeviceInfo(Guid deviceId);

  [Obsolete("Use RequestRemoteControlPermission2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> RequestRemoteControlPermission(Guid deviceId, int targetProcessId);

  [Obsolete("Use RequestRemoteControlSession3. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> RequestRemoteControlSession(Guid deviceId, RemoteControlSessionRequestDto sessionRequestDto);

  [Obsolete("Use RequestRemoteControlSession3. (deprecated 2026-10-20, v0.30.x)")]
  Task<HubResult> RequestRemoteControlSession2(RemoteControlSessionRequestDto sessionRequestDto);

  [Obsolete("Use RequestVncSession2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> RequestVncSession(Guid deviceId, VncSessionRequestDto sessionRequestDto);

  [Obsolete("Use SendAgentUpdateTrigger2. (deprecated 2026-09-03, v0.28.x)")]
  Task SendAgentUpdateTrigger(Guid deviceId);

  [Obsolete("Use SendChatMessage2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> SendChatMessage(Guid deviceId, ChatMessageHubDto dto);

  [Obsolete("Use SendPowerStateChange2. (deprecated 2026-09-03, v0.28.x)")]
  Task SendPowerStateChange(Guid deviceId, PowerStateChangeType changeType);

  [Obsolete("Use SendTerminalInput2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> SendTerminalInput(Guid deviceId, TerminalInputDto dto);

  [Obsolete("Use SendWakeDevice2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult<string>> SendWakeDevice(Guid deviceId, string[] macAddresses);

  [Obsolete("Use StartDeviceAccessActivity2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> StartDeviceAccessActivity(Guid deviceId);

  [Obsolete("Use SubscribeToDeviceHeartbeats2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> SubscribeToDeviceHeartbeats(Guid[] deviceIds);

  [Obsolete("Use TestVncConnection2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> TestVncConnection(Guid guid, int port);

  [Obsolete("Use UninstallAgent2. (deprecated 2026-09-03, v0.28.x)")]
  Task UninstallAgent(Guid deviceId, string reason);

  [Obsolete("Use UnsubscribeFromDeviceHeartbeats2. (deprecated 2026-09-03, v0.28.x)")]
  Task UnsubscribeFromDeviceHeartbeats(Guid[] deviceIds);
}
