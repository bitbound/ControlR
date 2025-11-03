using System.Runtime.CompilerServices;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Hubs.Clients;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

public class BrowserHubBase<TClient>(
  UserManager<AppUser> userManager,
  AppDb appDb,
  IAuthorizationService authorizationService,
  IHubContext<AgentHub, IAgentHubClient> agentHub,
  ILogger<BrowserHubBase<TClient>> logger) : HubWithItems<TClient>, IBrowserHubBase
  where TClient : class
{
  protected readonly IHubContext<AgentHub, IAgentHubClient> AgentHub = agentHub;
  protected readonly AppDb AppDb = appDb;
  protected readonly IAuthorizationService AuthorizationService = authorizationService;
  protected readonly ILogger<BrowserHubBase<TClient>> Logger = logger;
  protected readonly UserManager<AppUser> UserManager = userManager;

  public async Task RefreshDeviceInfo(Guid deviceId)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .RefreshDeviceInfo();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while refreshing device info.");
    }
  }

  public async Task SendDtoToAgent(Guid deviceId, DtoWrapper wrapper)
  {
    try
    {
      using var scope = Logger.BeginMemberScope();

      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveDto(wrapper);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending DTO to agent.");
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
      await AgentHub
        .Clients
        .Group(HubGroupNames.GetTenantDevicesGroupName(tenantId))
        .ReceiveDto(wrapper);
      return;
    }

    var user = await UserManager
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
    await AgentHub.Clients.Groups(groupNames).ReceiveDto(wrapper);
  }

  protected static Task<string> GetDisplayName(AppUser user, string fallbackName = "Admin")
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

  protected async Task<AppUser> GetRequiredUser(Func<IQueryable<AppUser>, IQueryable<AppUser>>? includeBuilder = null)
  {
    if (!TryGetUserId(out var userId))
    {
      throw new UnauthorizedAccessException("Failed to get user ID.");
    }

    var query = UserManager.Users.AsNoTracking();

    if (includeBuilder is not null)
    {
      query = includeBuilder.Invoke(query);
    }

    var user = await query.FirstOrDefaultAsync(x => x.Id == userId);

    Guard.IsNotNull(user);
    return user;
  }

  protected async Task<Result<Device>> TryAuthorizeAgainstDevice(
    Guid deviceId,
    [CallerMemberName] string? callerName = null)
  {
    if (Context.User is null)
    {
      Logger.LogCritical("User is null.  Authorize tag should have prevented this.");
      return Result.Fail<Device>("User is null.  Authorize tag should have prevented this.");
    }

    var device = await AppDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId);

    if (device is null)
    {
      Logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return Result.Fail<Device>("Device not found.");
    }

    var authResult = await AuthorizationService.AuthorizeAsync(
      Context.User,
      device,
      DeviceAccessByDeviceResourcePolicy.PolicyName);

    if (authResult.Succeeded)
    {
      return Result.Ok(device);
    }

    Logger.LogCritical(
      "Unauthorized agent access attempted by user: {UserName}.  Device: {DeviceId}.  Method: {MemberName}.",
      Context.UserIdentifier,
      deviceId,
      callerName);

    return Result.Fail<Device>("Unauthorized.");
  }

  protected bool TryGetTenantId(
    out Guid tenantId,
    [CallerMemberName] string callerName = "")
  {
    tenantId = Guid.Empty;
    if (Context.User?.TryGetTenantId(out tenantId) == true)
    {
      return true;
    }

    Logger.LogError("TenantId claim is unexpected missing when calling {MemberName}.", callerName);
    return false;
  }

  protected bool TryGetUserId(
    out Guid userId,
    [CallerMemberName] string callerName = "")
  {
    userId = Guid.Empty;
    if (Context.User?.TryGetUserId(out userId) == true)
    {
      return true;
    }

    Logger.LogError("UserId claim is unexpected missing when calling {MemberName}.", callerName);
    return false;
  }
}