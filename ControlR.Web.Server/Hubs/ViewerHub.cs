using System.Runtime.CompilerServices;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

[Authorize]
public class ViewerHub(
  UserManager<AppUser> userManager,
  AppDb appDb,
  IAuthorizationService authorizationService,
  IHubContext<AgentHub, IAgentHubClient> agentHub,
  IServerStatsProvider serverStatsProvider,
  IConnectionCounter connectionCounter,
  IIpApi ipApi,
  IWsBridgeApi wsBridgeApi,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<ViewerHub> logger) : HubWithItems<IViewerHubClient>, IViewerHub
{
  private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub = agentHub;
  private readonly AppDb _appDb = appDb;
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IAuthorizationService _authorizationService = authorizationService;
  private readonly IConnectionCounter _connectionCounter = connectionCounter;
  private readonly IIpApi _ipApi = ipApi;
  private readonly ILogger<ViewerHub> _logger = logger;
  private readonly IServerStatsProvider _serverStatsProvider = serverStatsProvider;
  private readonly UserManager<AppUser> _userManager = userManager;
  private readonly IWsBridgeApi _wsBridgeApi = wsBridgeApi;

  public Task<bool> CheckIfServerAdministrator()
  {
    return IsServerAdmin().AsTaskResult();
  }

  public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(
    string agentConnectionId,
    TerminalSessionRequest requestDto)
  {
    try
    {
      return await _agentHub.Clients
        .Client(agentConnectionId)
        .CreateTerminalSession(requestDto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail<TerminalSessionRequestResult>("An error occurred.");
    }
  }

  public async Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId)
  {
    try
    {
      return await _agentHub.Clients.Client(agentConnectionId).GetAgentAppSettings();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting agent appsettings.");
      return Result.Fail<AgentAppSettings>("Failed to get agent app settings.");
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

  public async Task<Uri?> GetWebSocketBridgeOrigin()
  {
    try
    {
      if (!_appOptions.CurrentValue.UseExternalWebSocketBridge ||
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
      if (closest.Origin is null || !await _wsBridgeApi.IsHealthy(closest.Origin))
      {
        return null;
      }

      return closest.Origin;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting WebSocket bridge URI.");
      return null;
    }
  }

  public async Task<WindowsSession[]> GetWindowsSessions(string agentConnectionId)
  {
    try
    {
      return await _agentHub.Clients.Client(agentConnectionId).GetWindowsSessions();
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

      _connectionCounter.IncrementViewerCount();
      await SendUpdatedConnectionCountToAdmins();

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

      _connectionCounter.DecrementViewerCount();
      await SendUpdatedConnectionCountToAdmins();

      if (Context.User is null)
      {
        return;
      }

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
    StreamerSessionRequestDto sessionRequestDto)
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
        "Starting streaming session requested by user {DisplayName} ({UserId}) for device {DeviceId} from IP {RemoteIp}.", 
        displayName,
        userId,
        deviceId,
        remoteIp);
      
      var device = await _appDb.Devices
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == deviceId);

      if (device is null)
      {
        return Result.Fail("Device not found.");
      }
      
      var authResult = await _authorizationService.AuthorizeAsync(
        Context.User, 
        device, 
        DeviceAccessByDeviceResourcePolicy.PolicyName);
      
      if (!authResult.Succeeded)
      {
        return Result.Fail("Unauthorized.");
      }
      
      if (string.IsNullOrWhiteSpace(displayName))
      {
        displayName = user.UserName ?? "";
      }

      // TODO: Convert to record.  Use with.
      sessionRequestDto.ViewerName = displayName;

      var sessionSuccess = await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateStreamingSession(sessionRequestDto);

      if (!sessionSuccess)
      {
        return Result.Fail("Failed to request a streaming session from the agent.");
      }

      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result.Fail(ex);
    }
  }

  public async Task<Result> SendAgentAppSettings(string agentConnectionId, AgentAppSettings appSettings)
  {
    try
    {
      return await _agentHub.Clients.Client(agentConnectionId).ReceiveAgentAppSettings(appSettings);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending agent appsettings.");
      return Result.Fail("Failed to send agent app settings.");
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

      var authResult = await TryAuthorizeAgainstDevice(deviceId);
      if (!authResult.IsSuccess)
      {
        return;
      }

      await _agentHub.Clients
        .Group(HubGroupNames.GetDeviceGroupName(deviceId, tenantId))
        .ReceiveDto(wrapper);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending DTO to agent.");
    }
  }

  public async Task SendDtoToUserGroups(DtoWrapper wrapper)
  {
    if (!TryGetUserId(out var userId))
    {
      return;
    }

    var user = await _userManager
      .Users
      .Include(x => x.Tags!)
      .ThenInclude(x => x.Devices)
      .FirstOrDefaultAsync(x => x.Id == userId);

    if (user?.Tags is null)
    {
      return;
    }

    var groupNames = user.Tags.Select(x => HubGroupNames.GetTagGroupName(x.Id, x.TenantId));
    await Clients.Groups(groupNames).ReceiveDto(wrapper);
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

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveTerminalInput(dto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending terminal input.");
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

  private bool IsServerAdmin()
  {
    return Context.User?.IsInRole(RoleNames.ServerAdministrator) ?? false;
  }

  private async Task SendUpdatedConnectionCountToAdmins()
  {
    try
    {
      var getResult = await _serverStatsProvider.GetServerStats();

      if (getResult.IsSuccess)
      {
        await Clients
          .Group(HubGroupNames.ServerAdministrators)
          .ReceiveServerStats(getResult.Value);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending updated agent connection count to admins.");
    }
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