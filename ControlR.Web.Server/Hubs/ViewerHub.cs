using System.Runtime.CompilerServices;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

[Authorize]
public class ViewerHub(
  UserManager<AppUser> userManager,
  AppDb appDb,
  IAuthorizationService authorizationService,
  IHubContext<AgentHub, IAgentHubClient> agentHub,
  IServerStatsProvider serverStatsProvider,
  IIpApi ipApi,
  IWsRelayApi wsRelayApi,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<ViewerHub> logger) : HubWithItems<IViewerHubClient>, IViewerHub
{
  private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub = agentHub;
  private readonly AppDb _appDb = appDb;
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IAuthorizationService _authorizationService = authorizationService;
  private readonly IIpApi _ipApi = ipApi;
  private readonly ILogger<ViewerHub> _logger = logger;
  private readonly IServerStatsProvider _serverStatsProvider = serverStatsProvider;
  private readonly UserManager<AppUser> _userManager = userManager;
  private readonly IWsRelayApi _wsRelayApi = wsRelayApi;

  public async Task<Result> CreateTerminalSession(
    Guid deviceId,
    TerminalSessionRequest requestDto)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Forbidden.");
      }

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CreateTerminalSession(requestDto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail("An error occurred.");
    }
  }

  public async Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail<PwshCompletionsResponseDto>("Forbidden.");
      }

      // Create new request with ViewerConnectionId
      var requestWithViewerConnection = request with { ViewerConnectionId = Context.ConnectionId };

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .GetPwshCompletions(requestWithViewerConnection);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting PowerShell command completions.");
      return Result.Fail<PwshCompletionsResponseDto>("An error occurred.");
    }
  }

  public async Task<Result<ServerStatsDto>> GetServerStats()
  {
    try
    {
      if (!VerifyIsServerAdmin())
      {
        return Result.Fail<ServerStatsDto>("Unauthorized.");
      }

      return await _serverStatsProvider.GetServerStats();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting agent count.");
      return Result.Fail<ServerStatsDto>("Failed to get agent count.");
    }
  }

  public async Task<Uri?> GetWebSocketRelayOrigin()
  {
    try
    {
      if (!_appOptions.CurrentValue.UseExternalWebSocketRelay ||
          _appOptions.CurrentValue.ExternalWebSocketHosts.Count == 0)
      {
        return null;
      }

      var ipAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
      if (string.IsNullOrWhiteSpace(ipAddress))
      {
        return null;
      }

      var result = await _ipApi.GetIpInfo(ipAddress);
      if (!result.IsSuccess)
      {
        return null;
      }

      var ipInfo = result.Value;

      if (ipInfo.Status == IpApiResponseStatus.Fail)
      {
        _logger.LogError("IpApi returned a failed status message.  Message: {IpMessage}", ipInfo.Message);
        return null;
      }

      var location = new Coordinate(ipInfo.Lat, ipInfo.Lon);
      var closest = CoordinateHelper.FindClosestCoordinate(location, _appOptions.CurrentValue.ExternalWebSocketHosts);
      if (closest.Origin is null || !await _wsRelayApi.IsHealthy(closest.Origin))
      {
        return null;
      }

      return closest.Origin;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting websocket relay URI.");
      return null;
    }
  }

  public async Task<DeviceUiSession[]> GetActiveUiSessions(Guid deviceId)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return [];
      }

      var device = authResult.Value;
      return await _agentHub.Clients.Client(device.ConnectionId).GetActiveUiSessions();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting Windows sessions from agent.");
      return [];
    }
  }

  public override async Task OnConnectedAsync()
  {
    try
    {
      await base.OnConnectedAsync();

      if (Context.User is null)
      {
        _logger.LogCritical("User is null.  Authorize tag should have prevented this.");
        return;
      }

      if (!Context.User.TryGetTenantId(out var tenantId))
      {
        _logger.LogCritical("Failed to get tenant ID.");
        return;
      }

      var user = await _userManager.GetUserAsync(Context.User);

      if (user is null)
      {
        _logger.LogCritical("Failed to find user from UserManager.");
        return;
      }

      user.IsOnline = true;
      await _userManager.UpdateAsync(user);

      if (Context.User.IsInRole(RoleNames.ServerAdministrator))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.ServerAdministrators);

        var getResult = await _serverStatsProvider.GetServerStats();
        if (getResult.IsSuccess)
        {
          await Clients.Caller.ReceiveServerStats(getResult.Value);
        }
      }

      if (Context.User.IsInRole(RoleNames.TenantAdministrator))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId,
          HubGroupNames.GetUserRoleGroupName(RoleNames.TenantAdministrator, tenantId));
      }

      if (Context.User.IsInRole(RoleNames.DeviceSuperUser))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId,
          HubGroupNames.GetUserRoleGroupName(RoleNames.DeviceSuperUser, tenantId));
      }
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

      if (Context.User is null)
      {
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

      if (Context.User.IsInRole(RoleNames.ServerAdministrator))
      {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroupNames.ServerAdministrators);
      }

      if (!Context.User.TryGetTenantId(out var tenantId))
      {
        return;
      }

      if (Context.User.IsInRole(RoleNames.TenantAdministrator))
      {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,
          HubGroupNames.GetUserRoleGroupName(RoleNames.TenantAdministrator, tenantId));
      }

      if (Context.User.IsInRole(RoleNames.DeviceSuperUser))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId,
          HubGroupNames.GetUserRoleGroupName(RoleNames.DeviceSuperUser, tenantId));
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during viewer disconnect.");
    }
  }

  public async Task<Result> RequestStreamingSession(
    Guid deviceId,
    RemoteControlSessionRequestDto sessionRequestDto)
  {
    try
    {
      if (Context.User is null)
      {
        return Result.Fail("User is null.");
      }

      if (!TryGetUserId(out var userId))
      {
        return Result.Fail("Failed to get user ID.");
      }

      var user = await _userManager.Users
        .AsNoTracking()
        .Include(x => x.UserPreferences)
        .FirstOrDefaultAsync(x => x.Id == userId);

      if (user is null)
      {
        return Result.Fail("User not found.");
      }

      var displayName = user.UserPreferences
        ?.FirstOrDefault(x => x.Name == UserPreferenceNames.UserDisplayName)
        ?.Value;

      if (string.IsNullOrWhiteSpace(displayName))
      {
        displayName = user.UserName ?? "";
      }

      var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

      _logger.LogInformation(
        "Starting streaming session requested by user {DisplayName} ({UserId}) for device {DeviceId} from IP {RemoteIp}.",
        displayName,
        userId,
        deviceId,
        remoteIp);

      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      var device = authResult.Value;

      var notifyUserSetting = _appDb.TenantSettings.FirstOrDefault(x => x.Name == TenantSettingsNames.NotifyUserOnSessionStart);
      if (notifyUserSetting is not null &&
          bool.TryParse(notifyUserSetting.Value, out var notifyUser))
      {
        sessionRequestDto = sessionRequestDto with { NotifyUserOnSessionStart = notifyUser };
      }

      sessionRequestDto = sessionRequestDto with { ViewerName = displayName };

      return await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateRemoteControlSession(sessionRequestDto);
    }
    catch (Exception ex)
    {
      return Result.Fail(ex);
    }
  }

  public async Task<Result> RequestVncSession(Guid deviceId, VncSessionRequestDto sessionRequestDto)
  {
    try
    {
      if (Context.User is null)
      {
        return Result.Fail("User is null.");
      }

      if (!TryGetUserId(out var userId))
      {
        return Result.Fail("Failed to get user ID.");
      }

      var user = await _userManager.Users
        .AsNoTracking()
        .Include(x => x.UserPreferences)
        .FirstOrDefaultAsync(x => x.Id == userId);

      if (user is null)
      {
        return Result.Fail("User not found.");
      }

      var displayName = user.UserPreferences
        ?.FirstOrDefault(x => x.Name == UserPreferenceNames.UserDisplayName)
        ?.Value;
      var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

      _logger.LogInformation(
        "Starting VNC session requested by user {DisplayName} ({UserId}) for device {DeviceId} from IP {RemoteIp}.",
        displayName,
        userId,
        deviceId,
        remoteIp);

      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      var device = authResult.Value;

      if (string.IsNullOrWhiteSpace(displayName))
      {
        displayName = user.UserName ?? "";
      }

      sessionRequestDto = sessionRequestDto with { ViewerName = displayName };

      return await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateVncSession(sessionRequestDto);
    }
    catch (Exception ex)
    {
      return Result.Fail(ex);
    }
  }

  public async Task SendDtoToAgent(Guid deviceId, DtoWrapper wrapper)
  {
    try
    {
      using var scope = _logger.BeginMemberScope();

      if (!TryGetTenantId(out var tenantId))
      {
        return;
      }

      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveDto(wrapper);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending DTO to agent.");
    }
  }

  public async Task SendDtoToUserGroups(DtoWrapper wrapper)
  {
    if (!TryGetUserId(out var userId) ||
        !TryGetTenantId(out var tenantId))
    {
      return;
    }

    if (Context.User!.IsInRole(RoleNames.DeviceSuperUser))
    {
      await _agentHub
        .Clients
        .Group(HubGroupNames.GetTenantDevicesGroupName(tenantId))
        .ReceiveDto(wrapper);
      return;
    }

    var user = await _userManager
      .Users
      .AsNoTracking()
      .Include(x => x.Tags!)
      .ThenInclude(x => x.Devices)
      .FirstOrDefaultAsync(x => x.Id == userId);

    if (user?.Tags is null)
    {
      return;
    }

    var groupNames = user.Tags.Select(x => HubGroupNames.GetTagGroupName(x.Id, x.TenantId));
    await _agentHub.Clients.Groups(groupNames).ReceiveDto(wrapper);
  }

  public async Task<Result> SendTerminalInput(Guid deviceId, TerminalInputDto dto)
  {
    try
    {
      var authResult = await TryAuthorizeAgainstDevice(deviceId);
      if (!authResult.IsSuccess)
      {
        return Result.Fail("User is not authorized to send terminal input.");
      }

      // Create new DTO with ViewerConnectionId
      var dtoWithViewerConnection = dto with { ViewerConnectionId = Context.ConnectionId };

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveTerminalInput(dtoWithViewerConnection);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending terminal input.");
      return Result.Fail("Agent could not be reached.");
    }
  }

  public async Task<Result> SendChatMessage(Guid deviceId, ChatMessageHubDto dto)
  {
    try
    {
      var authResult = await TryAuthorizeAgainstDevice(deviceId);
      if (!authResult.IsSuccess)
      {
        return Result.Fail("User is not authorized to send chat messages.");
      }

      // Log the chat message being sent
      _logger.LogInformation(
        "Chat message sent by user {SenderName} ({SenderEmail}) to device {DeviceId} for session {SessionId}",
        dto.SenderName,
        dto.SenderEmail,
        deviceId,
        dto.SessionId);

      var user = await GetRequiredUser(q => q.Include(u => u.UserPreferences));
      var displayName = await GetDisplayName(user, "Admin");
      dto = dto with
      {
        ViewerConnectionId = Context.ConnectionId,
        SenderName = displayName,
        SenderEmail = $"{user.Email}"
      };

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .SendChatMessage(dto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending chat message to agent.");
      return Result.Fail("Agent could not be reached.");
    }
  }

  public async Task<Result> CloseChatSession(Guid deviceId, Guid sessionId, int targetProcessId)
  {
    try
    {
      var authResult = await TryAuthorizeAgainstDevice(deviceId);
      if (!authResult.IsSuccess)
      {
        return Result.Fail("User is not authorized to close chat sessions.");
      }

      _logger.LogInformation(
        "Closing chat session {SessionId} for device {DeviceId} and process {ProcessId}",
        sessionId,
        deviceId,
        targetProcessId);

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CloseChatSession(sessionId, targetProcessId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while closing chat session {SessionId} on device {DeviceId}.", sessionId, deviceId);
      return Result.Fail("Agent could not be reached.");
    }
  }

  public async Task UninstallAgent(Guid deviceId, string reason)
  {
    try
    {
      var authResult = await TryAuthorizeAgainstDevice(deviceId);
      if (!authResult.IsSuccess)
      {
        return;
      }

      _logger.LogInformation(
        "Agent uninstall command sent by user: {UserName}.  Device: {DeviceId}",
        Context.UserIdentifier,
        deviceId);
      await _agentHub.Clients.Client(authResult.Value.ConnectionId).UninstallAgent(reason);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while uninstalling agent.");
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


  private bool IsServerAdmin()
  {
    return Context.User?.IsInRole(RoleNames.ServerAdministrator) ?? false;
  }

  private async Task<Result<Device>> TryAuthorizeAgainstDevice(
    Guid deviceId,
    [CallerMemberName] string? callerName = null)
  {
    if (Context.User is null)
    {
      _logger.LogCritical("User is null.  Authorize tag should have prevented this.");
      return Result.Fail<Device>("User is null.  Authorize tag should have prevented this.");
    }

    var device = await _appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId);

    if (device is null)
    {
      _logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return Result.Fail<Device>("Device not found.");
    }

    var authResult = await _authorizationService.AuthorizeAsync(
      Context.User,
      device,
      DeviceAccessByDeviceResourcePolicy.PolicyName);

    if (authResult.Succeeded)
    {
      return Result.Ok(device);
    }

    _logger.LogCritical(
      "Unauthorized agent access attempted by user: {UserName}.  Device: {DeviceId}.  Method: {MemberName}.",
      Context.UserIdentifier,
      deviceId,
      callerName);

    return Result.Fail<Device>("Unauthorized.");
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
  private bool VerifyIsServerAdmin([CallerMemberName] string callerMember = "")
  {
    if (IsServerAdmin())
    {
      return true;
    }

    var userName = Context.User?.Identity?.Name;
    _logger.LogCritical(
      "Admin verification failed when invoking member {MemberName}. User: {UserName}",
      callerMember,
      userName);

    return false;
  }
}