using System.Security.Claims;
using ControlR.Web.Server.Authn;

namespace ControlR.Web.Server.Extensions;

/// <summary>
/// Extension methods for identifying server service account principals.
/// </summary>
public static class ServerPrincipalExtensions
{
  /// <summary>
  /// Returns true when the principal is a server-scoped service account.
  /// </summary>
  public static bool IsServerPrincipal(this ClaimsPrincipal user)
  {
    return user.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value
      == PrincipalClaimTypes.ServerServiceAccount;
  }
}
