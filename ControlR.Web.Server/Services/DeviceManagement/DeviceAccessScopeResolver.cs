using System.Security.Claims;
using ControlR.Web.Server.Authn;

namespace ControlR.Web.Server.Services.DeviceManagement;

public interface IDeviceAccessScopeResolver
{
  Task<DeviceAccessScope> Resolve(ClaimsPrincipal user, Guid tenantId, CancellationToken cancellationToken = default);
}

public class DeviceAccessScopeResolver(AppDb appDb) : IDeviceAccessScopeResolver
{
  private readonly AppDb _appDb = appDb;

  public async Task<DeviceAccessScope> Resolve(
    ClaimsPrincipal user,
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    if (TryGetScopedDeviceId(user, out var deviceId))
    {
      return DeviceAccessScope.SingleDevice(deviceId);
    }

    if (user.IsInRole(RoleNames.TenantAdministrator) ||
        user.IsInRole(RoleNames.DeviceSuperUser))
    {
      return DeviceAccessScope.TenantWide();
    }

    if (!user.TryGetUserId(out var userId))
    {
      return DeviceAccessScope.None();
    }

    var tagIds = await _appDb.Users
      .Where(x => x.Id == userId && x.TenantId == tenantId)
      .SelectMany(x => x.Tags!.Select(tag => tag.Id))
      .ToListAsync(cancellationToken);

    return tagIds.Count == 0
      ? DeviceAccessScope.None()
      : DeviceAccessScope.TaggedDevices(tagIds);
  }

  private static bool TryGetScopedDeviceId(ClaimsPrincipal user, out Guid deviceId)
  {
    deviceId = Guid.Empty;

    var authMethod = user.FindFirst(UserClaimTypes.AuthenticationMethod)?.Value;
    if (authMethod != LogonTokenAuthenticationSchemeOptions.DefaultScheme)
    {
      return false;
    }

    var scopedDeviceIdValue = user.FindFirst(UserClaimTypes.DeviceSessionScope)?.Value;
    return Guid.TryParse(scopedDeviceIdValue, out deviceId);
  }
}