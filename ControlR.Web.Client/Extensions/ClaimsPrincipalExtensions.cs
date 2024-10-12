using ControlR.Web.Client.Authz;
using System.Security.Claims;

namespace ControlR.Web.Client.Extensions;

public static class ClaimsPrincipalExtensions
{
  public static bool IsAuthenticated(this ClaimsPrincipal user)
  {
    return user.Identity?.IsAuthenticated ?? false;
  }

  public static bool IsDeviceAdministrator(
    this ClaimsPrincipal user,
    Guid tenantUid)
  {
    if (!user.IsAuthenticated())
    {
      return false;
    }

    return user.HasClaim(x => 
      x.Type == UserClaimTypes.DeviceAdministrator &&
      x.Value == tenantUid.ToString());
  }

  public static bool TryGetTenantId(
    this ClaimsPrincipal user,
    out int tenantId)
  {
    tenantId = 0;
    if (!user.IsAuthenticated())
    {
      return false;
    }

    var tenantClaim = user.FindFirst(UserClaimTypes.TenantId);
    if (!int.TryParse(tenantClaim?.Value, out var id))
    {
      return false;
    }

    tenantId = id;
    return true;
  }

  public static bool TryGetTenantUid(
    this ClaimsPrincipal user,
    out Guid tenantUid)
  {
    tenantUid = Guid.Empty;
    if (!user.IsAuthenticated())
    {
      return false;
    }

    var tenantClaim = user.FindFirst(UserClaimTypes.TenantUid);
    if (!Guid.TryParse(tenantClaim?.Value, out var uid))
    {
      return false;
    }

    tenantUid = uid;
    return true;
  }
}