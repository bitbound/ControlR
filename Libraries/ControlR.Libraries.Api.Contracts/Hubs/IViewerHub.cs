using System.Threading.Channels;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.Api.Contracts.Hubs;

public interface IViewerHub
{
  [Obsolete("Use AddViewerActivity2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> AddViewerActivity(string activityName);

  Task<HubResult> AddViewerActivity2(AddViewerActivityRequestDto request);

  [Obsolete("Use CloseChatSession2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> CloseChatSession(Guid deviceId, Guid sessionId, int targetProcessId);
  Task<HubResult> CloseChatSession2(CloseChatSessionRequestDto request);

  [Obsolete("Use CloseTerminalSession2. (deprecated 2026-09-03, v0.28.x)")]
  Task CloseTerminalSession(Guid deviceId, Guid terminalSessionId);
  Task<HubResult> CloseTerminalSession2(CloseTerminalSessionRequestDto request);

  [Obsolete("Use CreateTerminalSession2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> CreateTerminalSession(
    Guid deviceId,
    Guid terminalSessionId);
  Task<HubResult> CreateTerminalSession2(CreateTerminalSessionRequestDto request);
  Task<HubResult> DisposeDeviceAccessActivity();

  [Obsolete("Use GetActiveDesktopSessions2. (deprecated 2026-09-03, v0.28.x)")]
  Task<DesktopSession[]> GetActiveDesktopSessions(Guid deviceId);
  Task<HubResult<ActiveDesktopSessionsResponseDto>> GetActiveDesktopSessions2(GetActiveDesktopSessionsRequestDto request);

  [Obsolete("Use GetDeviceAccessPermissions2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult<DeviceAccessPermissionsDto>> GetDeviceAccessPermissions(Guid deviceId);
  Task<HubResult<DeviceAccessPermissionsDto>> GetDeviceAccessPermissions2(GetDeviceAccessPermissionsRequestDto request);
  Task<HubResult<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request);

  [Obsolete("Use InvokeCtrlAltDel2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> InvokeCtrlAltDel(Guid deviceId, int targetDesktopProcessId, DesktopSessionType desktopSessionType);
  Task<HubResult> InvokeCtrlAltDel2(InvokeCtrlAltDelViewerRequestDto request);

  [Obsolete("Use RefreshDeviceInfo2. (deprecated 2026-09-03, v0.28.x)")]
  Task RefreshDeviceInfo(Guid deviceId);
  Task<HubResult> RefreshDeviceInfo2(RefreshDeviceInfoRequestDto request);

  [Obsolete("Use RequestRemoteControlPermission2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> RequestRemoteControlPermission(Guid deviceId, int targetProcessId);
  Task<HubResult> RequestRemoteControlPermission2(RequestRemoteControlPermissionRequestDto request);

  [Obsolete("Use RequestRemoteControlSession2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> RequestRemoteControlSession(Guid deviceId, RemoteControlSessionRequestDto sessionRequestDto);

  Task<HubResult> RequestRemoteControlSession2(RemoteControlSessionRequestDto sessionRequestDto);

  [Obsolete("Use RequestVncSession2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> RequestVncSession(Guid deviceId, VncSessionRequestDto sessionRequestDto);

  Task<HubResult> RequestVncSession2(VncSessionRequestDto sessionRequestDto);

  [Obsolete("Use SendAgentUpdateTrigger2. (deprecated 2026-09-03, v0.28.x)")]
  Task SendAgentUpdateTrigger(Guid deviceId);

  Task<HubResult> SendAgentUpdateTrigger2(SendAgentUpdateTriggerRequestDto request);

  [Obsolete("Use SendChatMessage2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> SendChatMessage(Guid deviceId, ChatMessageHubDto dto);

  Task<HubResult> SendChatMessage2(ChatMessageHubDto dto);

  Task<HubResult> SendDtoToAgent(SendDtoToAgentRequestDto request);

  [Obsolete("Use SendPowerStateChange2. (deprecated 2026-09-03, v0.28.x)")]
  Task SendPowerStateChange(Guid deviceId, PowerStateChangeType changeType);

  Task<HubResult> SendPowerStateChange2(SendPowerStateChangeRequestDto request);

  [Obsolete("Use SendTerminalInput2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> SendTerminalInput(Guid deviceId, TerminalInputDto dto);

  Task<HubResult> SendTerminalInput2(SendTerminalInputRequestDto request);

  [Obsolete("Use SendWakeDevice2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult<string>> SendWakeDevice(Guid deviceId, string[] macAddresses);

  Task<HubResult<string>> SendWakeDevice2(SendWakeDeviceRequestDto request);

  [Obsolete("Use StartDeviceAccessActivity2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> StartDeviceAccessActivity(Guid deviceId);

  Task<HubResult> StartDeviceAccessActivity2(StartDeviceAccessActivityRequestDto request);

  [Obsolete("Use SubscribeToDeviceHeartbeats2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> SubscribeToDeviceHeartbeats(Guid[] deviceIds);

  Task<HubResult> SubscribeToDeviceHeartbeats2(SubscribeToDeviceHeartbeatsRequestDto request);

  [Obsolete("Use TestVncConnection2. (deprecated 2026-09-03, v0.28.x)")]
  Task<HubResult> TestVncConnection(Guid guid, int port);

  Task<HubResult> TestVncConnection2(TestVncConnectionRequestDto request);

  [Obsolete("Use UninstallAgent2. (deprecated 2026-09-03, v0.28.x)")]
  Task UninstallAgent(Guid deviceId, string reason);

  Task<HubResult> UninstallAgent2(UninstallAgentRequestDto request);

  [Obsolete("Use UnsubscribeFromDeviceHeartbeats2. (deprecated 2026-09-03, v0.28.x)")]
  Task UnsubscribeFromDeviceHeartbeats(Guid[] deviceIds);

  Task<HubResult> UnsubscribeFromDeviceHeartbeats2(UnsubscribeFromDeviceHeartbeatsRequestDto request);

  Task<HubResult> UploadFile(FileUploadMetadata metadata, ChannelReader<byte[]> fileStream);
}