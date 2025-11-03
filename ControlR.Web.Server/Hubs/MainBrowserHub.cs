using System.Runtime.CompilerServices;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Hubs.Clients;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

[Authorize]
public class MainBrowserHub(
  UserManager<AppUser> userManager,
  AppDb appDb,
  IAuthorizationService authorizationService,
  IHubContext<AgentHub, IAgentHubClient> agentHub,
  IServerStatsProvider serverStatsProvider,
  ILogger<MainBrowserHub> logger) 
  : BrowserHubBase<IMainBrowserHubClient>(userManager, appDb, authorizationService, agentHub, logger), IMainBrowserHub
{
  private readonly IServerStatsProvider _serverStatsProvider = serverStatsProvider;

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
      Logger.LogError(ex, "Error while getting agent count.");
      return Result.Fail<ServerStatsDto>("Failed to get agent count.");
    }
  }
  public override async Task OnConnectedAsync()
  {
    try
    {
      await base.OnConnectedAsync();

      if (Context.User is null)
      {
        Logger.LogCritical("User is null.  Authorize tag should have prevented this.");
        return;
      }

      if (!Context.User.TryGetTenantId(out var tenantId))
      {
        Logger.LogCritical("Failed to get tenant ID.");
        return;
      }

      var user = await UserManager.GetUserAsync(Context.User);

      if (user is null)
      {
        Logger.LogCritical("Failed to find user from UserManager.");
        return;
      }

      user.IsOnline = true;
      await UserManager.UpdateAsync(user);

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
      Logger.LogError(ex, "Error during viewer connect.");
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

      var user = await UserManager.GetUserAsync(Context.User);

      if (user is null)
      {
        Logger.LogCritical("Failed to find user from UserManager.");
        return;
      }

      user.IsOnline = false;
      await UserManager.UpdateAsync(user);

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
      Logger.LogError(ex, "Error during viewer disconnect.");
    }
  }

  public async Task<Result> RequestVncSession(Guid deviceId, VncSessionRequestDto sessionRequestDto)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }
      
      if (Context.User is null)
      {
        return Result.Fail("User is null.");
      }

      if (!TryGetUserId(out var userId))
      {
        return Result.Fail("Failed to get user ID.");
      }

      var user = await UserManager.Users
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

      Logger.LogInformation(
        "Starting VNC session requested by user {DisplayName} ({UserId}) for device {DeviceId} from IP {RemoteIp}.",
        displayName,
        userId,
        deviceId,
        remoteIp);

      var device = authResult.Value;

      if (string.IsNullOrWhiteSpace(displayName))
      {
        displayName = user.UserName ?? "";
      }

      sessionRequestDto = sessionRequestDto with { ViewerName = displayName };

      return await AgentHub.Clients
        .Client(device.ConnectionId)
        .CreateVncSession(sessionRequestDto);
    }
    catch (Exception ex)
    {
      return Result.Fail(ex);
    }
  }

  public async Task SendAgentUpdateTrigger(Guid deviceId)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }
      
      await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveAgentUpdateTrigger();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending agent update trigger.");
    }
  }

  public async Task SendPowerStateChange(Guid deviceId, PowerStateChangeType changeType)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }
      
      await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceivePowerStateChange(changeType);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending power state change.");
    }
  }

  public async Task SendWakeDevice(Guid deviceId, string[] macAddresses)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      var tagsIds = await AppDb.Devices
        .Include(x => x.Tags)
        .Where(x => x.Id == deviceId)
        .SelectMany(x => x.Tags!)
        .Select(x => x.Id)
        .ToListAsync();
      
      var tagGroupNames = tagsIds.Select(tagId => 
        HubGroupNames.GetTagGroupName(tagId, authResult.Value.TenantId));
      
      var dto = new WakeDeviceDto(macAddresses);
      await AgentHub.Clients
        .Groups(tagGroupNames)
        .InvokeWakeDevice(dto);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending wake device command.");
    }
  }

  public async Task UninstallAgent(Guid deviceId, string reason)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      Logger.LogInformation(
        "Agent uninstall command sent by user: {UserName}.  Device: {DeviceId}",
        Context.UserIdentifier,
        deviceId);
        
      await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .UninstallAgent(reason);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while uninstalling agent.");
    }
  }

  private bool IsServerAdmin()
  {
    return Context.User?.IsInRole(RoleNames.ServerAdministrator) ?? false;
  }

  private bool VerifyIsServerAdmin([CallerMemberName] string callerMember = "")
  {
    if (IsServerAdmin())
    {
      return true;
    }

    var userName = Context.User?.Identity?.Name;
    Logger.LogCritical(
      "Admin verification failed when invoking member {MemberName}. User: {UserName}",
      callerMember,
      userName);

    return false;
  }
}