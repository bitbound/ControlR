using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace ControlR.Web.Client.Extensions;

public static class ClaimsPrincipalExtensions
{
  public static bool IsAuthenticated(this ClaimsPrincipal user)
  {
    return user.Identity?.IsAuthenticated ?? false;
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