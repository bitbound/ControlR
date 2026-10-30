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
    PermissionNames.DeviceRemoteControlInteract,
    PermissionNames.DeviceRemoteControlPrivacyScreen
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
    return RequestRemoteControlSessionCore(sessionRequestDto);
  }

  private async Task<HubResult> RequestRemoteControlSessionCore(RemoteControlSessionRequestDto sessionRequestDto)
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

      var commandPolicy = RemoteControlCommandPolicyDto.Create(
        sessionRequestDto.SessionId,
        RemoteControlCommandPermissions.GetAllowedDtoTypes(allowedPermissions));

      sessionRequestDto = sessionRequestDto with
      {
        NotifyUserOnSessionStart = notifyUser,
        ViewerConnectionId = Context.ConnectionId,
        ViewerName = displayName,
        CommandPolicy = commandPolicy
      };

      var result = await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateRemoteControlSession(sessionRequestDto);

      if (!result.IsSuccess)
      {
        return HubResult.Fail(
          result.Reason ?? "The agent failed to start the remote control session.",
          result.FailureCode);
      }

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      const string reason = "An error occurred while requesting the remote control session.";
      _logger.LogError(ex, reason);
      return HubResult.Fail(reason);
    }
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
      await _deviceConnectionRegistry.Set(device.Id, device.ConnectionId, cancellationToken);
    }

    return cache.GetOrAdd(device.Id, allowedPermissions);
  }
}
