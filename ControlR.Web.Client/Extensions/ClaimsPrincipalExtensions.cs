using System.Security.Claims;

namespace ControlR.Web.Client.Extensions;

public static class ClaimsPrincipalExtensions
{
  public static bool IsAdministrator(this ClaimsPrincipal user)
  {
    return user.HasClaim(x => x is { Type: ClaimNames.IsAdministrator });
  }

  public static bool IsAuthenticated(this ClaimsPrincipal user)
  {
    return user.Identity?.IsAuthenticated ?? false;
  }
}