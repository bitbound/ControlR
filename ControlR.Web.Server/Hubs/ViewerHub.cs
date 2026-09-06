using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using Microsoft.AspNetCore.SignalR;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using System.Diagnostics;
using System.Security.Claims;
using ControlR.Web.Server.Services.Authorization.Capabilities;
using System.Collections.Immutable;

namespace ControlR.Web.Server.Hubs;

[Authorize]
public class ViewerHub(
  TimeProvider timeProvider,
  UserManager<AppUser> userManager,
  AppDb appDb,
  IAuthorizationService authorizationService,
  IPermissionEvaluator permissionEvaluator,
  IResourceDescriptorFactory resourceFactory,
  IHubContext<AgentHub, IAgentHubClient> agentHub,
  IEffectiveUserPreferencesResolver effectiveUserPreferencesResolver,
  IHubStreamStore hubStreamStore,
  IDesktopSessionAccessAuthorizer desktopSessionAccessAuthorizer,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<ViewerHub> logger)
  : HubWithItems<IViewerHubClient>, IViewerHub
{
  private const int MaxHeartbeatSubscriptionBatch = 100;

  private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub = agentHub;
  private readonly AppDb _appDb = appDb;
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IAuthorizationService _authorizationService = authorizationService;
  private readonly IDesktopSessionAccessAuthorizer _desktopSessionAccessAuthorizer = desktopSessionAccessAuthorizer;
  private readonly IEffectiveUserPreferencesResolver _effectiveUserPreferencesResolver = effectiveUserPreferencesResolver;
  private readonly IHubStreamStore _hubStreamStore = hubStreamStore;
  private readonly ILogger<ViewerHub> _logger = logger;
  private readonly IPermissionEvaluator _permissionEvaluator = permissionEvaluator;
  private readonly IResourceDescriptorFactory _resourceFactory = resourceFactory;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly UserManager<AppUser> _userManager = userManager;

  public Activity? SessionActivity
  {
    get => GetItem((Activity?)null);
    set => SetItem(value);
  }

  [Obsolete("Use AddViewerActivity2. (deprecated 2026-09-03, v0.28.x)")]
  public Task<HubResult> AddViewerActivity(string activityName)
  {
    return AddViewerActivity2(new(activityName));
  }

  public Task<HubResult> AddViewerActivity2(AddViewerActivityRequestDto request)
  {
    using var activity = SessionActivity?.StartChildActivity(request.ActivityName);
    _logger.LogInformation("Viewer Activity: {EventName}", request.ActivityName);
    return Task.FromResult(HubResult.Ok());
  }

  [Obsolete("Use CloseChatSession2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> CloseChatSession(Guid deviceId, Guid sessionId, int targetProcessId)
  {
    return await CloseChatSession2(new(deviceId, sessionId, targetProcessId));
  }

  public async Task<HubResult> CloseChatSession2(CloseChatSessionRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.ChatSend) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      _logger.LogInformation(
        "Closing chat session {SessionId} for device {DeviceId} and process {ProcessId}",
        request.SessionId,
        request.DeviceId,
        request.TargetProcessId);

      var result = await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CloseChatSession(request.SessionId, request.TargetProcessId);

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while closing chat session {SessionId} on device {DeviceId}.", request.SessionId, request.DeviceId);
      return HubResult.Fail("Agent could not be reached.");
    }
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

  public async Task<HubResult> CloseTerminalSession2(CloseTerminalSessionRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.TerminalUse) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Forbidden.");
      }

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CloseTerminalSession(request.TerminalId);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while closing terminal session.");
      return HubResult.Fail("An error occurred.");
    }
  }

  [Obsolete("Use CreateTerminalSession2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> CreateTerminalSession(
    Guid deviceId,
    Guid terminalSessionId)
  {
    return await CreateTerminalSession2(new(deviceId, terminalSessionId));
  }

  public async Task<HubResult> CreateTerminalSession2(CreateTerminalSessionRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.TerminalUse) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Forbidden.");
      }

      var createResult = await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CreateTerminalSession(request.TerminalId, Context.ConnectionId);

      _logger.LogInformation("Create terminal session.  Success: {IsSuccess}", createResult.IsSuccess);

      return createResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating terminal session.");
      return HubResult.Fail("An error occurred.");
    }
  }

  public Task<HubResult> DisposeDeviceAccessActivity()
  {
    SessionActivity?.Dispose();
    SessionActivity = null;
    return Task.FromResult(HubResult.Ok());
  }

  [Obsolete("Use GetActiveDesktopSessions2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<DesktopSession[]> GetActiveDesktopSessions(Guid deviceId)
  {
    var result = await GetActiveDesktopSessions2(new(deviceId));
    return result.IsSuccess
      ? result.Value?.ToArray() ?? []
      : [];
  }

  public async Task<HubResult<IReadOnlyList<DesktopSession>>> GetActiveDesktopSessions2(GetActiveDesktopSessionsRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.RemoteControlConnect) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail<IReadOnlyList<DesktopSession>>("Unauthorized.");
      }

      var device = authResult.Value;
      var principal = Context.User?.ToPrincipalDescriptor();
      if (principal is null)
      {
        return HubResult.Fail<IReadOnlyList<DesktopSession>>("Unauthorized.");
      }

      var sessions = await _agentHub.Clients.Client(device.ConnectionId).GetActiveDesktopSessions();
      var authorizedSessions = sessions.Where(x => _desktopSessionAccessAuthorizer
        .CanUse(principal, request.DeviceId, x.SystemSessionId))
        .ToImmutableList();

      return HubResult.Ok<IReadOnlyList<DesktopSession>>(
        [.. authorizedSessions]);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting Windows sessions from agent.");
      return HubResult.Fail<IReadOnlyList<DesktopSession>>("An error occurred.");
    }
  }

  [Obsolete("Use GetDeviceAccessPermissions2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult<DeviceAccessPermissionsDto>> GetDeviceAccessPermissions(Guid deviceId)
  {
    return await GetDeviceAccessPermissions2(new(deviceId));
  }

  public async Task<HubResult<DeviceAccessPermissionsDto>> GetDeviceAccessPermissions2(GetDeviceAccessPermissionsRequestDto request)
  {
    try
    {
      var device = await _appDb.Devices
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == request.DeviceId);

      if (device is null)
      {
        return HubResult.Fail<DeviceAccessPermissionsDto>("Device not found.");
      }

      var principal = Context.User?.ToPrincipalDescriptor();
      if (principal is null)
      {
        return HubResult.Fail<DeviceAccessPermissionsDto>("Unauthorized.");
      }

      var resource = await _resourceFactory.CreateDevice(device, Context.ConnectionAborted);
      var permissionNames = new[]
      {
        PermissionNames.DeviceRead,
        PermissionNames.DeviceOverviewRead,
        PermissionNames.DeviceRemoteControlConnect,
        PermissionNames.DeviceTerminalUse,
        PermissionNames.DeviceChatSend,
        PermissionNames.DeviceFileSystemRead,
        PermissionNames.DeviceLogsRead,
        PermissionNames.DeviceVncRelayConnect,
        PermissionNames.DeviceRemoteControlInteract,
        PermissionNames.DeviceRemoteControlBlockInput,
        PermissionNames.DeviceClipboardRead,
        PermissionNames.DeviceClipboardWrite,
        PermissionNames.DeviceCtrlAltDelSend
      };
      var decisions = await _permissionEvaluator.EvaluateMany(
        principal,
        permissionNames,
        resource,
        Context.ConnectionAborted);

      if (!decisions[PermissionNames.DeviceRead].Allowed)
      {
        return HubResult.Fail<DeviceAccessPermissionsDto>("Unauthorized.");
      }

      var permissions = new DeviceAccessPermissionsDto(
        decisions[PermissionNames.DeviceOverviewRead].Allowed,
        decisions[PermissionNames.DeviceRemoteControlConnect].Allowed,
        decisions[PermissionNames.DeviceTerminalUse].Allowed,
        decisions[PermissionNames.DeviceChatSend].Allowed,
        decisions[PermissionNames.DeviceFileSystemRead].Allowed,
        decisions[PermissionNames.DeviceLogsRead].Allowed,
        decisions[PermissionNames.DeviceVncRelayConnect].Allowed,
        decisions[PermissionNames.DeviceRemoteControlInteract].Allowed,
        decisions[PermissionNames.DeviceRemoteControlBlockInput].Allowed,
        decisions[PermissionNames.DeviceClipboardRead].Allowed,
        decisions[PermissionNames.DeviceClipboardWrite].Allowed,
        decisions[PermissionNames.DeviceCtrlAltDelSend].Allowed);

      return HubResult.Ok(permissions);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while resolving device-access permissions for device {DeviceId}.", request.DeviceId);
      return HubResult.Fail<DeviceAccessPermissionsDto>("An error occurred while resolving device permissions.");
    }
  }

  public async Task<HubResult<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.TerminalUse) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail<PwshCompletionsResponseDto>("Forbidden.");
      }

      // Create a new request with ViewerConnectionId
      var requestWithViewerConnection = request with { ViewerConnectionId = Context.ConnectionId };

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .GetPwshCompletions(requestWithViewerConnection);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting PowerShell command completions.");
      return HubResult.Fail<PwshCompletionsResponseDto>("An error occurred.");
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

  public override async Task OnConnectedAsync()
  {
    try
    {
      await base.OnConnectedAsync();

      if (Context.User?.TryGetUserId(out var userId) != true)
      {
        _logger.LogCritical("User is null on connect. Client is trying to connect to ViewerHub from an authenticated but invalid context.");
        return;
      }

      var user = await _appDb.Users.FirstOrDefaultAsync(x => x.Id == userId);
      if (user is null)
      {
        _logger.LogCritical("Failed to find user from UserManager.");
        return;
      }

      user.IsOnline = true;
      user.LastLogin = _timeProvider.GetUtcNow();
      await _appDb.SaveChangesAsync();

      await JoinServerTopics();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during viewer connect.");
    }
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    try
    {
      await base.OnDisconnectedAsync(exception);

      SessionActivity?.Dispose();
      SessionActivity = null;

      if (Context.User is null)
      {
        _logger.LogCritical("User is null on disconnect. The principal may have been invalidated during the connection lifetime.");
        return;
      }

      var user = await _userManager.GetUserAsync(Context.User);

      if (user is null)
      {
        _logger.LogCritical("Failed to find user from UserManager.");
        return;
      }

      user.IsOnline = false;
      await _userManager.UpdateAsync(user);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during viewer disconnect.");
    }
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

  public async Task<HubResult> RefreshDeviceInfo2(RefreshDeviceInfoRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.Read) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .RefreshDeviceInfo();

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while refreshing device info.");
      return HubResult.Fail("An error occurred while refreshing device info.");
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

      if (await TryAuthorizeAgainstDevice(sessionRequestDto.DeviceId, DeviceResourcePolicies.RemoteControlConnect) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      if (!CanUseDesktopSession(sessionRequestDto.DeviceId, sessionRequestDto.TargetSystemSession))
      {
        return HubResult.Fail("The requested desktop session is not authorized.");
      }

      var device = authResult.Value;
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

  [Obsolete("Use RequestVncSession2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> RequestVncSession(
    Guid deviceId,
    VncSessionRequestDto sessionRequestDto)
  {
    return await RequestVncSession2(
      sessionRequestDto with { DeviceId = deviceId });
  }

  public async Task<HubResult> RequestVncSession2(VncSessionRequestDto sessionRequestDto)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(sessionRequestDto.DeviceId, DeviceResourcePolicies.VncRelayConnect) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      if (Context.User is null)
      {
        return HubResult.Fail("User is null.");
      }

      if (!TryGetUserId(out var userId))
      {
        return HubResult.Fail("Failed to get user ID.");
      }

      var user = await _userManager.Users
        .AsNoTracking()
        .Include(x => x.UserPreferences)
        .FirstOrDefaultAsync(x => x.Id == userId);

      if (user is null)
      {
        return HubResult.Fail("User not found.");
      }

      var notifyUser = await _effectiveUserPreferencesResolver.GetNotifyUserOnSessionStart(
        authResult.Value.TenantId,
        userId,
        Context.ConnectionAborted);

      var displayNameResult = await GetDisplayName(userId);
      if (!displayNameResult.IsSuccess)
      {
        return HubResult.Fail(displayNameResult.Reason ?? "Failed to resolve display name.");
      }

      var displayName = displayNameResult.Value;
      
      var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

      _logger.LogInformation(
        "Starting VNC session requested by user {DisplayName} ({UserId}) for device {DeviceId} from IP {RemoteIp}.",
        displayName,
        userId,
        sessionRequestDto.DeviceId,
        remoteIp);

      var device = authResult.Value;

      if (string.IsNullOrWhiteSpace(displayName))
      {
        displayName = user.UserName ?? "";
      }

      sessionRequestDto = sessionRequestDto with
      {
        NotifyUserOnSessionStart = notifyUser,
        ViewerConnectionId = Context.ConnectionId,
        ViewerName = displayName,
      };

      return await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateVncSession(sessionRequestDto);
    }
    catch (Exception ex)
    {
      const string reason = "An error occurred while requesting the VNC session.";
      _logger.LogError(ex, reason);
      return HubResult.Fail(reason);
    }
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

  public async Task<HubResult> SendAgentUpdateTrigger2(SendAgentUpdateTriggerRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.AgentUpdate) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveAgentUpdateTrigger();

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending agent update trigger.");
      return HubResult.Fail("An error occurred while sending the agent update trigger.");
    }
  }

  [Obsolete("Use SendChatMessage2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> SendChatMessage(Guid deviceId, ChatMessageHubDto dto)
  {
    return await SendChatMessage2(dto with { DeviceId = deviceId });
  }

  public async Task<HubResult> SendChatMessage2(ChatMessageHubDto dto)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(dto.DeviceId, DeviceResourcePolicies.ChatSend) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      if (!CanUseDesktopSession(dto.DeviceId, dto.TargetSystemSession))
      {
        return HubResult.Fail("The requested desktop session is not authorized.");
      }

      var user = await GetRequiredUser(q => q.Include(u => u.UserPreferences));
      var displayName = await GetDisplayName(user);

      // Log the chat message being sent
      _logger.LogInformation(
        "Chat message sent by user {SenderName} ({SenderEmail}) to device {DeviceId} for session {SessionId}",
        displayName,
        user.Email,
        dto.DeviceId,
        dto.SessionId);

      dto = dto with
      {
        ViewerConnectionId = Context.ConnectionId,
        SenderName = displayName,
        SenderEmail = $"{user.Email}"
      };

      var sendResult = await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .SendChatMessage(dto);

      return sendResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending chat message to agent.");
      return HubResult.Fail("Agent could not be reached.");
    }
  }

  public async Task<HubResult> SendDtoToAgent(SendDtoToAgentRequestDto request)
  {
    try
    {
      using var scope = _logger.BeginMemberScope();

      if (await TryAuthorizeAgainstDevice(request.DeviceId) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveDto(request.Wrapper);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending DTO to agent.");
      return HubResult.Fail("An error occurred while sending the DTO to the agent.");
    }
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

  public async Task<HubResult> SendPowerStateChange2(SendPowerStateChangeRequestDto request)
  {
    try
    {
      if (request.ChangeType is PowerStateChangeType.None)
      {
        return HubResult.Fail("Invalid power state change type.");
      }

      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.PowerManage) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceivePowerStateChange(request.ChangeType);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending power state change.");
      return HubResult.Fail("An error occurred while sending the power state change.");
    }
  }

  [Obsolete("Use SendTerminalInput2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> SendTerminalInput(Guid deviceId, TerminalInputDto dto)
  {
    return await SendTerminalInput2(new(deviceId, dto));
  }

  public async Task<HubResult> SendTerminalInput2(SendTerminalInputRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.TerminalUse) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      // Create a new DTO with ViewerConnectionId
      var dtoWithViewerConnection = request.Input with { ViewerConnectionId = Context.ConnectionId };

      var sendResult = await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveTerminalInput(dtoWithViewerConnection);

      _logger.LogInformation("Terminal input sent to agent. Success: {Success}", sendResult.IsSuccess);

      return sendResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending terminal input.");
      return HubResult.Fail("Agent could not be reached.");
    }
  }

  [Obsolete("Use SendWakeDevice2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult<string>> SendWakeDevice(Guid deviceId, string[] macAddresses)
  {
    return await SendWakeDevice2(new(deviceId, macAddresses));
  }

  public async Task<HubResult<string>> SendWakeDevice2(SendWakeDeviceRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.WakeSend) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail<string>("Unauthorized.");
      }

      var target = authResult.Value;

      if (string.IsNullOrWhiteSpace(target.PublicIpV4))
      {
        return HubResult.Ok<string>("The target device has no known public IP, so no network neighbors could be found to broadcast the magic packet.");
      }

      var connectionIds = await _appDb.Devices
        .Where(device => device.Id != request.DeviceId &&
                         device.TenantId == target.TenantId &&
                         device.CustomerId == target.CustomerId &&
                         device.PublicIpV4 == target.PublicIpV4 &&
                         device.IsOnline &&
                         device.ConnectionId != string.Empty)
        .Select(device => device.ConnectionId)
        .ToListAsync();

      if (connectionIds.Count == 0)
      {
        return HubResult.Ok<string>($"No online devices sharing public IP {target.PublicIpV4} were found. The target may need an online agent on the same network to be woken.");
      }

      var dto = new WakeDeviceDto([.. request.MacAddresses]);
      await _agentHub.Clients
        .Clients(connectionIds)
        .InvokeWakeDevice(dto);

      return HubResult.Ok<string>($"Magic packet broadcast by {connectionIds.Count} devic{(connectionIds.Count == 1 ? "e" : "es")} with public IP {target.PublicIpV4}.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending wake device command.");
      return HubResult.Fail<string>("An error occurred while sending the wake command.");
    }
  }

  [Obsolete("Use StartDeviceAccessActivity2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> StartDeviceAccessActivity(Guid deviceId)
  {
    return await StartDeviceAccessActivity2(new(deviceId));
  }

  public async Task<HubResult> StartDeviceAccessActivity2(StartDeviceAccessActivityRequestDto request)
  {
    if (Context.User is null)
    {
      _logger.LogCritical("Failed to get user ID when starting remote access session.");
      return HubResult.Fail("Unauthorized.");
    }

    var authResult = await TryAuthorizeAgainstDevice(request.DeviceId);
    if (!authResult.IsSuccess)
    {
      return HubResult.Fail("Unauthorized.");
    }

    var user = await _userManager.GetUserAsync(Context.User);

    if (user?.UserName is null)
    {
      _logger.LogCritical("Failed to get user name when starting remote access session.");
      return HubResult.Fail("Unauthorized.");
    }

    SessionActivity = DefaultActivitySource.StartDeviceAccessActivity(
      userName: user.UserName,
      userId: user.Id,
      deviceId: request.DeviceId);

    if (Context.User.FindFirstValue(UserClaimTypes.SessionCorrelationId) is {} sessionCorrelationId)
    {
      SessionActivity?.SetTag(ActivityTagKeys.SessionCorrelationId, sessionCorrelationId);
    }

    return HubResult.Ok();
  }

  [Obsolete("Use SubscribeToDeviceHeartbeats2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> SubscribeToDeviceHeartbeats(Guid[] deviceIds)
  {
    return await SubscribeToDeviceHeartbeats2(new(deviceIds));
  }

  public async Task<HubResult> SubscribeToDeviceHeartbeats2(SubscribeToDeviceHeartbeatsRequestDto request)
  {
    var deviceIds = request.DeviceIds;
    if (Context.User is null)
    {
      return HubResult.Fail("Not authenticated.");
    }

    if (deviceIds is not { Count: > 0 })
    {
      return HubResult.Ok();
    }

    if (deviceIds.Count > MaxHeartbeatSubscriptionBatch)
    {
      return HubResult.Fail(
        $"Too many device IDs ({deviceIds.Count}). Subscribe at most {MaxHeartbeatSubscriptionBatch} devices per call.");
    }

    var distinctIds = deviceIds.Distinct().ToList();

    var devices = await _appDb.Devices.AsNoTracking()
      .Include(x => x.DeviceGroupMembers)
      .Where(x => distinctIds.Contains(x.Id))
      .ToListAsync();

    var principal = Context.User.ToPrincipalDescriptor();
    if (principal is null)
    {
      return HubResult.Fail("Invalid principal.");
    }

    var requests = new List<PermissionEvaluationRequest>(devices.Count);
    foreach (var device in devices)
    {
      requests.Add(new PermissionEvaluationRequest(
        PermissionNames.DeviceRead,
        await _resourceFactory.CreateDevice(device, Context.ConnectionAborted)));
    }

    var results = await _permissionEvaluator.EvaluateBatch(
      principal,
      requests,
      Context.ConnectionAborted);

    for (var index = 0; index < results.Count; index++)
    {
      if (results[requests[index]].Allowed)
      {
        var deviceId = requests[index].Resource.Id!.Value;
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.DeviceHeartbeat(deviceId));
      }
    }

    return HubResult.Ok();
  }

  [Obsolete("Use TestVncConnection2. (deprecated 2026-09-03, v0.28.x)")]
  public async Task<HubResult> TestVncConnection(Guid guid, int port)
  {
    return await TestVncConnection2(new(guid, port));
  }

  public async Task<HubResult> TestVncConnection2(TestVncConnectionRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.VncRelayConnect) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .TestVncConnection(request.Port);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while testing VNC connection.");
      return HubResult.Fail("An error occurred while testing the VNC connection.");
    }
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

  public async Task<HubResult> UninstallAgent2(UninstallAgentRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId, DeviceResourcePolicies.Delete) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      _logger.LogInformation(
        "Agent uninstall command sent by user: {UserName}.  Device: {DeviceId}",
        Context.UserIdentifier,
        request.DeviceId);

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .UninstallAgent(request.Reason);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while uninstalling agent.");
      return HubResult.Fail("An error occurred while uninstalling the agent.");
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

  public async Task<HubResult> UnsubscribeFromDeviceHeartbeats2(UnsubscribeFromDeviceHeartbeatsRequestDto request)
  {
    var deviceIds = request.DeviceIds;
    if (deviceIds is not { Count: > 0 })
    {
      return HubResult.Ok();
    }

    foreach (var deviceId in deviceIds.Distinct())
    {
      await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroupNames.DeviceHeartbeat(deviceId));
    }

    return HubResult.Ok();
  }

  public async Task<HubResult> UploadFile(
    FileUploadMetadata fileUploadMetadata,
    ChannelReader<byte[]> fileStream)
  {
    try
    {

      var deviceId = fileUploadMetadata.DeviceId;

      if (await TryAuthorizeAgainstDevice(deviceId, DeviceResourcePolicies.FileSystemWrite) is not { IsSuccess: true } authResult)
      {
        return HubResult.Fail("Unauthorized.");
      }

      var maxUploadSize = _appOptions.CurrentValue.MaxFileTransferSize;
      if (maxUploadSize > 0 && fileUploadMetadata.FileSize > maxUploadSize)
      {
        return HubResult.Fail($"File size exceeds the maximum allowed size of {maxUploadSize} bytes.");
      }

      var device = authResult.Value;
      if (string.IsNullOrWhiteSpace(device.ConnectionId))
      {
        _logger.LogWarning("Device {DeviceId} is not connected (no ConnectionId).", deviceId);
        return HubResult.Fail("Device is not currently connected.");
      }

      var streamId = Guid.NewGuid();
      using var signaler = _hubStreamStore.GetOrCreate<byte[]>(streamId, TimeSpan.FromMinutes(30));

      var uploadRequest = new FileUploadHubDto(
        streamId,
        fileUploadMetadata.TargetDirectory,
        fileUploadMetadata.FileName,
        fileUploadMetadata.FileSize,
        fileUploadMetadata.Overwrite);

      // Asynchronously write the client's stream to the channel.
      var writeTask = signaler.WriteFromChannelReader(fileStream, Context.ConnectionAborted);

      // Notify the agent about the incoming upload
      var receiveResult = await _agentHub.Clients
        .Client(device.ConnectionId)
        .DownloadFileFromViewer(uploadRequest)
        .WaitAsync(Context.ConnectionAborted);

      if (receiveResult is null || !receiveResult.IsSuccess)
      {
        var reason = receiveResult?.Reason ?? "Agent did not respond.";
        _logger.LogWarning("Device {DeviceId} failed to download file {FileName}.  Reason: {Reason}",
          deviceId,
          fileUploadMetadata.FileName,
          reason);
        return HubResult.Fail($"Agent failed to download file: {reason}");
      }

      // Await the write task to ensure all data is sent or an error occurs.
      try
      {
        await writeTask;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error writing file stream for {FileName} to device {DeviceId}",
          fileUploadMetadata.FileName, fileUploadMetadata.DeviceId);
        return HubResult.Fail("An error occurred while writing the file stream.");
      }

      return HubResult.Ok();
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("File upload was canceled by the user for file {FileName} to device {DeviceId}",
        fileUploadMetadata.FileName,
        fileUploadMetadata.DeviceId);
      return HubResult.Fail("File upload was canceled.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error uploading file {FileName} to device {DeviceId}",
        fileUploadMetadata.FileName, fileUploadMetadata.DeviceId);
      return HubResult.Fail("An error occurred during file upload.");
    }
  }

  private static Task<string> GetDisplayName(AppUser user, string fallbackName = "Admin")
  {
    var displayName = user.UserPreferences
      ?.FirstOrDefault(x => x.Name == UserPreferenceNames.UserDisplayName)
      ?.Value;

    if (string.IsNullOrWhiteSpace(displayName))
    {
      displayName = user.UserName ?? fallbackName;
    }

    return displayName.AsTaskResult();
  }

  private bool CanUseDesktopSession(Guid deviceId, int systemSessionId)
  {
    var principal = Context.User?.ToPrincipalDescriptor();
    return principal is not null &&
      _desktopSessionAccessAuthorizer.CanUse(principal, deviceId, systemSessionId);
  }

  private async Task<HubResult<string>> GetDisplayName(Guid userId)
  {
    var user = await _userManager.Users
      .AsNoTracking()
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.Id == userId);

    if (user is null)
    {
      _logger.LogError("User not found.");
      return HubResult.Fail<string>("User not found.");
    }

    var displayName = user.UserPreferences
      ?.FirstOrDefault(x => x.Name == UserPreferenceNames.UserDisplayName)
      ?.Value;

    if (string.IsNullOrWhiteSpace(displayName))
    {
      displayName = user.UserName ?? "";
    }
    return HubResult.Ok(displayName);
  }

  private async Task<AppUser> GetRequiredUser(Func<IQueryable<AppUser>, IQueryable<AppUser>>? includeBuilder = null)
  {
    if (!TryGetUserId(out var userId))
    {
      throw new UnauthorizedAccessException("Failed to get user ID.");
    }

    var query = _userManager.Users.AsNoTracking();

    if (includeBuilder is not null)
    {
      query = includeBuilder.Invoke(query);
    }

    var user = await query.FirstOrDefaultAsync(x => x.Id == userId);

    Guard.IsNotNull(user);
    return user;
  }

  private async Task JoinServerTopics()
  {
    if (Context.User is null)
    {
      return;
    }

    var principal = Context.User?.ToPrincipalDescriptor();
    if (principal is null)
    {
      return;
    }

    var serverResource = new ResourceDescriptor(PermissionScopeKind.Server);
    var decisions = await _permissionEvaluator.EvaluateMany(
      principal,
      [PermissionNames.ServerAlertsRead, PermissionNames.ServerTelemetryRead],
      serverResource,
      Context.ConnectionAborted);
    if (decisions[PermissionNames.ServerAlertsRead].Allowed)
    {
      await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.ServerAlerts());
    }

    if (decisions[PermissionNames.ServerTelemetryRead].Allowed)
    {
      await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.ServerTelemetry());
    }
  }

  private async Task<HubResult<Device>> TryAuthorizeAgainstDevice(
    Guid deviceId,
    string? policyName = null,
    [CallerMemberName] string? callerName = null)
  {
    if (Context.User is null)
    {
      _logger.LogCritical("User is null.  Authorize tag should have prevented this.");
      return HubResult.Fail<Device>("User is null.  Authorize tag should have prevented this.");
    }

    var device = await _appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId);

    if (device is null)
    {
      _logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return HubResult.Fail<Device>("Device not found.");
    }

    var authResult = await _authorizationService.AuthorizeAsync(
      Context.User,
      device,
      policyName ?? DeviceResourcePolicies.Read);

    if (authResult.Succeeded)
    {
      return HubResult.Ok(device);
    }

    _logger.LogCritical(
      "Unauthorized agent access attempted by user: {UserName}.  Device: {DeviceId}.  Method: {MemberName}.",
      Context.UserIdentifier,
      deviceId,
      callerName);

    return HubResult.Fail<Device>("Unauthorized.");
  }

  private bool TryGetTenantId(
    out Guid tenantId,
    [CallerMemberName] string callerName = "")
  {
    tenantId = Guid.Empty;
    if (Context.User?.TryGetTenantId(out tenantId) == true)
    {
      return true;
    }

    _logger.LogError("TenantId claim is unexpected missing when calling {MemberName}.", callerName);
    return false;
  }

  private bool TryGetUserId(
    out Guid userId,
    [CallerMemberName] string callerName = "")
  {
    userId = Guid.Empty;
    if (Context.User?.TryGetUserId(out userId) == true)
    {
      return true;
    }

    _logger.LogError("UserId claim is unexpected missing when calling {MemberName}.", callerName);
    return false;
  }
}
