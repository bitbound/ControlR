// Deprecated hub methods kept so that a consumer shipping on its own schedule can still call them.
// Each one forwards to its numbered successor.  Nothing new belongs here.

using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

namespace ControlR.Web.Server.Hubs;

public partial class ViewerHub
{
  [Obsolete("Use AddViewerActivity2. (deprecated 2026-09-03, v0.28.x)")]
  public Task<HubResult> AddViewerActivity(string activityName)
  {
    return AddViewerActivity2(new(activityName));
  }

  [Obsolete("Use CloseChatSession2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> CloseChatSession(Guid deviceId, Guid sessionId, int targetProcessId)
  {
    return await CloseChatSession2(new(deviceId, sessionId, targetProcessId));
  }

  [Obsolete("Use CloseTerminalSession2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task CloseTerminalSession(Guid deviceId, Guid terminalSessionId)
  {
    var result = await CloseTerminalSession2(new(deviceId, terminalSessionId));
    if (!result.IsSuccess)
    {
      _logger.LogWarning("CloseTerminalSession failed: {Reason}", result.Reason);
    }
  }

  [Obsolete("Use CreateTerminalSession2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> CreateTerminalSession(
    Guid deviceId,
    Guid terminalSessionId)
  {
    return await CreateTerminalSession2(new(deviceId, terminalSessionId));
  }

  [Obsolete("Use GetActiveDesktopSessions2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<DesktopSession[]> GetActiveDesktopSessions(Guid deviceId)
  {
    var result = await GetActiveDesktopSessions2(new(deviceId));
    return result.IsSuccess
      ? result.Value?.Sessions.ToArray() ?? []
      : [];
  }

  [Obsolete("Use GetDeviceAccessPermissions2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult<DeviceAccessPermissionsDto>> GetDeviceAccessPermissions(Guid deviceId)
  {
    return await GetDeviceAccessPermissions2(new(deviceId));
  }

  [Obsolete("Use InvokeCtrlAltDel2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> InvokeCtrlAltDel(Guid deviceId, int targetDesktopProcessId, DesktopSessionType desktopSessionType)
  {
    return await InvokeCtrlAltDel2(new(deviceId, targetDesktopProcessId, desktopSessionType));
  }

  [Obsolete("Use RefreshDeviceInfo2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task RefreshDeviceInfo(Guid deviceId)
  {
    var result = await RefreshDeviceInfo2(new(deviceId));
    if (!result.IsSuccess)
    {
      _logger.LogWarning("RefreshDeviceInfo failed: {Reason}", result.Reason);
    }
  }

  [Obsolete("Use RequestRemoteControlPermission2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> RequestRemoteControlPermission(Guid deviceId, int targetProcessId)
  {
    return await RequestRemoteControlPermission2(new(deviceId, targetProcessId));
  }

  [Obsolete("Use RequestRemoteControlSession3. (deprecated 2026-09-03, v0.28.x)")]
  public Task<HubResult> RequestRemoteControlSession(
    Guid deviceId,
    RemoteControlSessionRequestDto sessionRequestDto)
  {
    return RequestRemoteControlSessionCore(
      sessionRequestDto with { DeviceId = deviceId },
      false);
  }

  [Obsolete("Use RequestRemoteControlSession3. (deprecated 2026-10-20, v0.30.x)")]
  public Task<HubResult> RequestRemoteControlSession2(RemoteControlSessionRequestDto sessionRequestDto)
  {
    return RequestRemoteControlSessionCore(sessionRequestDto, false);
  }

  [Obsolete("Use RequestVncSession2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> RequestVncSession(
    Guid deviceId,
    VncSessionRequestDto sessionRequestDto)
  {
    return await RequestVncSession2(
      sessionRequestDto with { DeviceId = deviceId });
  }

  [Obsolete("Use SendAgentUpdateTrigger2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task SendAgentUpdateTrigger(Guid deviceId)
  {
    var result = await SendAgentUpdateTrigger2(new(deviceId));
    if (!result.IsSuccess)
    {
      _logger.LogWarning("SendAgentUpdateTrigger failed: {Reason}", result.Reason);
    }
  }

  [Obsolete("Use SendChatMessage2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> SendChatMessage(Guid deviceId, ChatMessageHubDto dto)
  {
    return await SendChatMessage2(dto with { DeviceId = deviceId });
  }

  [Obsolete("Use SendPowerStateChange2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task SendPowerStateChange(Guid deviceId, PowerStateChangeType changeType)
  {
    var result = await SendPowerStateChange2(new(deviceId, changeType));
    if (!result.IsSuccess)
    {
      _logger.LogWarning("SendPowerStateChange failed: {Reason}", result.Reason);
    }
  }

  [Obsolete("Use SendTerminalInput2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> SendTerminalInput(Guid deviceId, TerminalInputDto dto)
  {
    return await SendTerminalInput2(new(deviceId, dto));
  }

  [Obsolete("Use SendWakeDevice2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult<string>> SendWakeDevice(Guid deviceId, string[] macAddresses)
  {
    return await SendWakeDevice2(new(deviceId, macAddresses));
  }

  [Obsolete("Use StartDeviceAccessActivity2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> StartDeviceAccessActivity(Guid deviceId)
  {
    return await StartDeviceAccessActivity2(new(deviceId));
  }

  [Obsolete("Use SubscribeToDeviceHeartbeats2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> SubscribeToDeviceHeartbeats(Guid[] deviceIds)
  {
    return await SubscribeToDeviceHeartbeats2(new(deviceIds));
  }

  [Obsolete("Use TestVncConnection2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> TestVncConnection(Guid guid, int port)
  {
    return await TestVncConnection2(new(guid, port));
  }

  [Obsolete("Use UninstallAgent2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task UninstallAgent(Guid deviceId, string reason)
  {
    var result = await UninstallAgent2(new(deviceId, reason));
    if (!result.IsSuccess)
    {
      _logger.LogWarning("UninstallAgent failed: {Reason}", result.Reason);
    }
  }

  [Obsolete("Use UnsubscribeFromDeviceHeartbeats2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task UnsubscribeFromDeviceHeartbeats(Guid[] deviceIds)
  {
    var result = await UnsubscribeFromDeviceHeartbeats2(new(deviceIds));
    if (!result.IsSuccess)
    {
      _logger.LogWarning("UnsubscribeFromDeviceHeartbeats failed: {Reason}", result.Reason);
    }
  }
}
