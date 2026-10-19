using System.Collections.Concurrent;
using System.Collections.Immutable;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization.Capabilities;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

public partial class ViewerHub
{
  private static readonly string[] RemoteControlPermissionNames =
  [
    PermissionNames.DeviceClipboardRead,
    PermissionNames.DeviceClipboardWrite,
    PermissionNames.DeviceRemoteControlBlockInput,
    PermissionNames.DeviceRemoteControlConnect,
    PermissionNames.DeviceRemoteControlInteract
  ];

  // Commands arrive at input rate, so the session's permissions are resolved once and reused.
  private ConcurrentDictionary<Guid, RemoteControlAuthorization> RemoteControlAuthorizations
  {
    get
    {
      lock (Context.Items)
      {
        if (GetItem<ConcurrentDictionary<Guid, RemoteControlAuthorization>?>(null) is { } cached)
        {
          return cached;
        }

        var created = new ConcurrentDictionary<Guid, RemoteControlAuthorization>();
        SetItem(created);
        return created;
      }
    }
  }

  [Obsolete("Use GetActiveDesktopSessions2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<DesktopSession[]> GetActiveDesktopSessions(Guid deviceId)
  {
    var result = await GetActiveDesktopSessions2(new(deviceId));
    return result.IsSuccess
      ? result.Value?.Sessions.ToArray() ?? []
      : [];
  }

  public async Task<HubResult<ActiveDesktopSessionsResponseDto>> GetActiveDesktopSessions2(GetActiveDesktopSessionsRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.RemoteControlConnect) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail<ActiveDesktopSessionsResponseDto>("Unauthorized.");
      }

      var device = authResult.Value;
      var principal = Context.User?.ToPrincipalDescriptor();
      if (principal is null)
      {
        return HubResult.Fail<ActiveDesktopSessionsResponseDto>("Unauthorized.");
      }

      var sessions = await _agentHub.Clients.Client(device.ConnectionId).GetActiveDesktopSessions();
      var authorizedSessions = sessions.Where(x => _desktopSessionAccessAuthorizer
        .CanUse(principal, request.DeviceId, x.SystemSessionId))
        .ToImmutableList();

      return HubResult.Ok(new ActiveDesktopSessionsResponseDto(authorizedSessions));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting Windows sessions from agent.");
      return HubResult.Fail<ActiveDesktopSessionsResponseDto>("An error occurred.");
    }
  }

  [Obsolete("Use InvokeCtrlAltDel2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> InvokeCtrlAltDel(Guid deviceId, int targetDesktopProcessId, DesktopSessionType desktopSessionType)
  {
    return await InvokeCtrlAltDel2(new(deviceId, targetDesktopProcessId, desktopSessionType));
  }

  public async Task<HubResult> InvokeCtrlAltDel2(InvokeCtrlAltDelViewerRequestDto request)
  {
    try
    {
      _logger.LogInformation(
        "Invoking CtrlAltDel for device {DeviceId} and process {ProcessId}.  User: {UserId}",
        request.DeviceId,
        request.TargetDesktopProcessId,
        Context.UserIdentifier);

      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.CtrlAltDelSend) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      if (!TryGetUserId(out var userId))
      {
        _logger.LogError("Failed to get user ID for CtrlAltDel invocation.");
        return HubResult.Fail("Failed to get user ID.");
      }

      var displayNameResult = await GetDisplayName(userId);
      if (!displayNameResult.IsSuccess)
      {
        return HubResult.Fail(displayNameResult.Reason ?? "Failed to resolve display name.");
      }

      var dto = new InvokeCtrlAltDelRequestDto(
        request.TargetDesktopProcessId,
        Context.User?.Identity?.Name ?? "Unknown",
        request.DesktopSessionType);

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .InvokeCtrlAltDel(dto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "An error occurred while invoking CtrlAltDel.");
      return HubResult.Fail("An error occurred while invoking CtrlAltDel.");
    }
  }

  [Obsolete("Use RequestRemoteControlPermission2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> RequestRemoteControlPermission(Guid deviceId, int targetProcessId)
  {
    return await RequestRemoteControlPermission2(new(deviceId, targetProcessId));
  }

  public async Task<HubResult> RequestRemoteControlPermission2(RequestRemoteControlPermissionRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.RemoteControlConnect) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .RequestRemoteControlPermission(request.TargetProcessId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while requesting remote control permission.");
      return HubResult.Fail("An error occurred while requesting remote control permission.");
    }
  }

  [Obsolete("Use RequestRemoteControlSession2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> RequestRemoteControlSession(
    Guid deviceId,
    RemoteControlSessionRequestDto sessionRequestDto)
  {
    return await RequestRemoteControlSession2(
      sessionRequestDto with { DeviceId = deviceId });
  }

  public async Task<HubResult> RequestRemoteControlSession2(
    RemoteControlSessionRequestDto sessionRequestDto)
  {
    try
    {
      if (!TryGetUserId(out var userId))
      {
        return HubResult.Fail("Failed to get user ID.");
      }

      var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

      var displayNameResult = await GetDisplayName(userId);
      if (!displayNameResult.IsSuccess)
      {
        return HubResult.Fail(displayNameResult.Reason ?? "Failed to resolve display name.");
      }

      var displayName = displayNameResult.Value;

      _logger.LogInformation(
        "Starting streaming session requested by user {DisplayName} ({UserId}) for device {DeviceId} from IP {RemoteIp}.",
        displayName,
        userId,
        sessionRequestDto.DeviceId,
        remoteIp);

      if (!CanUseDesktopSession(sessionRequestDto.DeviceId, sessionRequestDto.TargetSystemSession))
      {
        return HubResult.Fail("The requested desktop session is not authorized.");
      }

      var authorization = await TryGetRemoteControlAuthorization(
        sessionRequestDto.DeviceId,
        sessionRequestDto.SessionId,
        Context.ConnectionAborted);

      if (authorization is null ||
          !authorization.AllowedPermissions.Contains(PermissionNames.DeviceRemoteControlConnect))
      {
        _logger.LogCritical(
          "Unauthorized remote control session attempted by user: {UserName}.  Device: {DeviceId}.",
          Context.UserIdentifier,
          sessionRequestDto.DeviceId);
        return HubResult.Fail("Unauthorized.");
      }

      var device = authorization.Device;
      var notifyUser = await _effectiveUserPreferencesResolver.GetNotifyUserOnSessionStart(
        device.TenantId,
        userId,
        Context.ConnectionAborted);

      sessionRequestDto = sessionRequestDto with
      {
        NotifyUserOnSessionStart = notifyUser,
        ViewerName = displayName,
        ViewerConnectionId = Context.ConnectionId
      };

      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateRemoteControlSession(sessionRequestDto);

      return result;
    }
    catch (Exception ex)
    {
      const string reason = "An error occurred while requesting the remote control session.";
      _logger.LogError(ex, reason);
      return HubResult.Fail(reason);
    }
  }

  public async Task<HubResult> SendRemoteControlDtoToAgent(SendRemoteControlDtoToAgentRequestDto request)
  {
    try
    {
      using var scope = _logger.BeginMemberScope();

      var authorization = await TryGetRemoteControlAuthorization(
        request.DeviceId,
        request.SessionId,
        Context.ConnectionAborted);

      var permission = GetRemoteControlPermission(request.Wrapper.DtoType);
      if (authorization is null ||
          !authorization.AllowedPermissions.Contains(permission))
      {
        _logger.LogCritical(
          "Unauthorized remote control action attempted by user: {UserName}.  Device: {DeviceId}.  " +
          "Session: {SessionId}.  DtoType: {DtoType}.",
          Context.UserIdentifier,
          request.DeviceId,
          request.SessionId,
          request.Wrapper.DtoType);
        return HubResult.Fail("Unauthorized.");
      }

      return await _agentHub.Clients
        .Client(authorization.Device.ConnectionId)
        .ReceiveRemoteControlDto(request);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending remote control DTO to agent.");
      return HubResult.Fail("An error occurred while sending the remote control DTO to the agent.");
    }
  }

  private static string GetRemoteControlPermission(RemoteControlDtoType dtoType) => dtoType switch
  {
    RemoteControlDtoType.ClipboardText => PermissionNames.DeviceClipboardWrite,
    RemoteControlDtoType.RequestClipboardText => PermissionNames.DeviceClipboardRead,
    RemoteControlDtoType.ToggleBlockInput => PermissionNames.DeviceRemoteControlBlockInput,
    RemoteControlDtoType.RequestKeyFrame
      or RemoteControlDtoType.UpdateCaptureSettings
      or RemoteControlDtoType.CloseRemoteControlSession => PermissionNames.DeviceRemoteControlConnect,
    _ => PermissionNames.DeviceRemoteControlInteract
  };

  private async Task<RemoteControlAuthorization?> TryGetRemoteControlAuthorization(
    Guid deviceId,
    Guid sessionId,
    CancellationToken cancellationToken)
  {
    var cache = RemoteControlAuthorizations;
    if (cache.TryGetValue(sessionId, out var cached))
    {
      return cached;
    }

    if (Context.User?.ToPrincipalDescriptor() is not { } principal)
    {
      return null;
    }

    var device = await _appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);

    if (device is null)
    {
      _logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return null;
    }

    var resource = await _resourceFactory.CreateDevice(device, cancellationToken);
    var decisions = await _permissionEvaluator.EvaluateMany(
      principal,
      RemoteControlPermissionNames,
      resource,
      cancellationToken);

    var allowedPermissions = decisions
      .Where(x => x.Value.Allowed)
      .Select(x => x.Key)
      .ToHashSet(StringComparer.Ordinal);

    return cache.GetOrAdd(sessionId, new RemoteControlAuthorization(device, allowedPermissions));
  }

  private sealed record RemoteControlAuthorization(
    Device Device,
    IReadOnlySet<string> AllowedPermissions);
}
