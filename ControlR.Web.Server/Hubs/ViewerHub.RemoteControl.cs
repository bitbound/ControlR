using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Claims;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Web.Server.Authz.Permissions;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

public partial class ViewerHub
{
  private static readonly string[] _remoteControlPermissionNames =
  [
    PermissionNames.DeviceClipboardRead,
    PermissionNames.DeviceClipboardWrite,
    PermissionNames.DeviceRemoteControlBlockInput,
    PermissionNames.DeviceRemoteControlConnect,
    PermissionNames.DeviceRemoteControlInteract
  ];

  // Commands arrive at input rate, so the device's permissions are resolved once and reused.
  // Keyed by device alone because every input of the evaluation is fixed for the connection,
  // so a session-supplied discriminator could only add entries holding an identical answer.
  private ConcurrentDictionary<Guid, IReadOnlySet<string>> RemoteControlAuthorizations
  {
    get
    {
      lock (Context.Items)
      {
        if (GetItem<ConcurrentDictionary<Guid, IReadOnlySet<string>>?>(null) is { } cached)
        {
          return cached;
        }

        var created = new ConcurrentDictionary<Guid, IReadOnlySet<string>>();
        SetItem(created);
        return created;
      }
    }
  }

  // Session ids that this connection started. Per-command requests carry a session id that the
  // viewer supplies, so without this a viewer holding a device permission could address another
  // viewer's live session on the same device. Lives in Context.Items, which SignalR discards when
  // the connection ends.
  private ConcurrentDictionary<(Guid DeviceId, Guid SessionId), byte> RemoteControlSessionBindings
  {
    get
    {
      lock (Context.Items)
      {
        if (GetItem<ConcurrentDictionary<(Guid DeviceId, Guid SessionId), byte>?>(null) is { } cached)
        {
          return cached;
        }

        var created = new ConcurrentDictionary<(Guid DeviceId, Guid SessionId), byte>();
        SetItem(created);
        return created;
      }
    }
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

  public async Task<HubResult> InvokeRemoteControlDtoOnAgent(SendRemoteControlDtoToAgentRequestDto request)
  {
    try
    {
      using var scope = _logger.BeginMemberScope();

      var authorization = await TryAuthorizeRemoteControlDto(request, Context.ConnectionAborted);
      if (!authorization.IsSuccess)
      {
        return HubResult.Fail(authorization.Reason, authorization.FailureCode);
      }

      return await _agentHub.Clients
        .Client(authorization.Value)
        .InvokeRemoteControlDto(request)
        .WaitAsync(Context.ConnectionAborted);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking remote control DTO on agent.");
      return HubResult.Fail("An error occurred while sending the remote control DTO to the agent.");
    }
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

  public Task<HubResult> RequestRemoteControlSession3(RemoteControlSessionRequestDto sessionRequestDto)
  {
    return RequestRemoteControlSessionCore(sessionRequestDto, true);
  }

  public async Task<HubResult> SendRemoteControlDtoToAgent(SendRemoteControlDtoToAgentRequestDto request)
  {
    try
    {
      using var scope = _logger.BeginMemberScope();

      var authorization = await TryAuthorizeRemoteControlDto(request, Context.ConnectionAborted);
      if (!authorization.IsSuccess)
      {
        _logger.LogWarning(
          "Remote control DTO {DtoType} for session {SessionId} was not sent to device {DeviceId}.  {Reason}",
          request.Wrapper.DtoType,
          request.SessionId,
          request.DeviceId,
          authorization.Reason);
        return HubResult.Fail(authorization.Reason, authorization.FailureCode);
      }

      await _agentHub.Clients
        .Client(authorization.Value)
        .SendRemoteControlDto(request);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending remote control DTO to agent.");
      return HubResult.Fail("An error occurred while sending the remote control DTO to the agent.");
    }
  }

  private static string GetRemoteControlPermission(RemoteControlCommandDtoType dtoType) => dtoType switch
  {
    RemoteControlCommandDtoType.ClipboardText => PermissionNames.DeviceClipboardWrite,
    RemoteControlCommandDtoType.RequestClipboardText => PermissionNames.DeviceClipboardRead,
    RemoteControlCommandDtoType.ToggleBlockInput => PermissionNames.DeviceRemoteControlBlockInput,
    RemoteControlCommandDtoType.RequestKeyFrame
      or RemoteControlCommandDtoType.UpdateCaptureSettings
      or RemoteControlCommandDtoType.CloseRemoteControlSession => PermissionNames.DeviceRemoteControlConnect,
    _ => PermissionNames.DeviceRemoteControlInteract
  };

  private async Task<HubResult> RequestRemoteControlSessionCore(
    RemoteControlSessionRequestDto sessionRequestDto,
    bool viewerSupportsRemoteControlDtoHub)
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

      var device = await _appDb.Devices
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == sessionRequestDto.DeviceId, Context.ConnectionAborted);

      if (device is null)
      {
        _logger.LogWarning("Device {DeviceId} not found.", sessionRequestDto.DeviceId);
        return HubResult.Fail("Device not found.");
      }

      if (Context.User is not { } user)
      {
        return HubResult.Fail("Unauthorized.");
      }

      var allowedPermissions = await TryGetRemoteControlPermissions(
        device,
        user,
        Context.ConnectionAborted);

      if (allowedPermissions is null ||
          !allowedPermissions.Contains(PermissionNames.DeviceRemoteControlConnect))
      {
        _logger.LogCritical(
          "Unauthorized remote control session attempted by user: {UserName}.  Device: {DeviceId}.",
          Context.UserIdentifier,
          sessionRequestDto.DeviceId);
        return HubResult.Fail("Unauthorized.");
      }

      if (!device.IsOnline || string.IsNullOrWhiteSpace(device.ConnectionId))
      {
        _logger.LogWarning("Device {DeviceId} is not online.", sessionRequestDto.DeviceId);
        return HubResult.Fail("Device is not currently online.", OperationFailureCode.DeviceOffline);
      }

      var notifyUser = await _effectiveUserPreferencesResolver.GetNotifyUserOnSessionStart(
        device.TenantId,
        userId,
        Context.ConnectionAborted);

      sessionRequestDto = sessionRequestDto with
      {
        NotifyUserOnSessionStart = notifyUser,
        ViewerConnectionId = Context.ConnectionId,
        ViewerName = displayName,
        ViewerSupportsRemoteControlDtoHub = viewerSupportsRemoteControlDtoHub
      };

      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateRemoteControlSession(sessionRequestDto);

      if (result.IsSuccess)
      {
        RemoteControlSessionBindings.TryAdd(
          (sessionRequestDto.DeviceId, sessionRequestDto.SessionId),
          0);
      }

      return result;
    }
    catch (Exception ex)
    {
      const string reason = "An error occurred while requesting the remote control session.";
      _logger.LogError(ex, reason);
      return HubResult.Fail(reason);
    }
  }

  private async Task<HubResult<string>> TryAuthorizeRemoteControlDto(
    SendRemoteControlDtoToAgentRequestDto request,
    CancellationToken cancellationToken)
  {
    if (Context.User is not { } user)
    {
      return HubResult.Fail<string>("Unauthorized.");
    }

    var permission = GetRemoteControlPermission(request.Wrapper.DtoType);
    var cache = RemoteControlAuthorizations;
    if (!cache.TryGetValue(request.DeviceId, out var allowedPermissions))
    {
      var device = await _appDb.Devices
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == request.DeviceId, cancellationToken);

      if (device is null)
      {
        _logger.LogWarning("Device {DeviceId} not found.", request.DeviceId);
        return HubResult.Fail<string>("Device not found.");
      }

      allowedPermissions = await TryGetRemoteControlPermissions(
        device,
        user,
        cancellationToken);
    }

    if (allowedPermissions is null || !allowedPermissions.Contains(permission))
    {
      _logger.LogCritical(
        "Unauthorized remote control action attempted by user: {UserName}.  Device: {DeviceId}.  " +
        "Session: {SessionId}.  DtoType: {DtoType}.",
        Context.UserIdentifier,
        request.DeviceId,
        request.SessionId,
        request.Wrapper.DtoType);
      return HubResult.Fail<string>("Unauthorized.");
    }

    if (!RemoteControlSessionBindings.ContainsKey((request.DeviceId, request.SessionId)))
    {
      _logger.LogWarning(
        "Remote control DTO {DtoType} was refused for a session this connection did not start.  " +
        "User: {UserName}.  Device: {DeviceId}.  Session: {SessionId}.",
        request.Wrapper.DtoType,
        Context.UserIdentifier,
        request.DeviceId,
        request.SessionId);
      return HubResult.Fail<string>("Unauthorized.");
    }

    // Steady state reads nothing from the database. AgentHub keeps the registry current on connect,
    // heartbeat, and disconnect; the database read below only covers this server starting up or the
    // agent being connected by another node.
    if (_deviceConnectionRegistry.TryGetConnectionId(request.DeviceId, out var agentConnectionId))
    {
      return HubResult.Ok(agentConnectionId);
    }

    var trackedDevice = await _appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == request.DeviceId, cancellationToken);

    if (trackedDevice is null)
    {
      _logger.LogWarning("Device {DeviceId} not found.", request.DeviceId);
      return HubResult.Fail<string>("Device not found.");
    }

    if (!trackedDevice.IsOnline || string.IsNullOrWhiteSpace(trackedDevice.ConnectionId))
    {
      _logger.LogWarning("Device {DeviceId} is not online.", request.DeviceId);
      return HubResult.Fail<string>("Device is not currently online.", OperationFailureCode.DeviceOffline);
    }

    _deviceConnectionRegistry.Set(trackedDevice.Id, trackedDevice.ConnectionId);
    return HubResult.Ok(trackedDevice.ConnectionId);
  }

  private async Task<IReadOnlySet<string>?> TryGetRemoteControlPermissions(
    Device device,
    ClaimsPrincipal user,
    CancellationToken cancellationToken)
  {
    var cache = RemoteControlAuthorizations;
    if (cache.TryGetValue(device.Id, out var cached))
    {
      return cached;
    }

    if (user.ToPrincipalDescriptor() is not { } principal)
    {
      return null;
    }

    var resource = await _resourceFactory.CreateDevice(device, cancellationToken);
    var decisions = await _permissionEvaluator.EvaluateMany(
      principal,
      _remoteControlPermissionNames,
      resource,
      cancellationToken);

    var allowedPermissions = decisions
      .Where(x => x.Value.Allowed)
      .Select(x => x.Key)
      .ToHashSet(StringComparer.Ordinal);

    if (device.IsOnline && !string.IsNullOrWhiteSpace(device.ConnectionId))
    {
      _deviceConnectionRegistry.Set(device.Id, device.ConnectionId);
    }

    return cache.GetOrAdd(device.Id, allowedPermissions);
  }
}
