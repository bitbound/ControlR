using System.Security.Claims;
using ControlR.Web.Server.Authn;

namespace ControlR.Web.Server.Extensions;

/// <summary>
/// Server-side principal helpers. Kept separate from the shared
/// <see cref="ControlR.Web.Client.Extensions.ClaimsPrincipalExtensions"/> (which is also globally
/// imported) to avoid extension-method ambiguity, and because the service account never reaches the client.
/// </summary>
public static class ServerPrincipalExtensions
{
  /// <summary>
  /// Returns true when the principal is a server-scoped service account. This is the lightest
  /// possible check: a single claim read, no service location or DB call. A server service account
  /// has unbound (cross-tenant) access in the interim authorization model.
  /// </summary>
  public static bool IsServerPrincipal(this ClaimsPrincipal user)
  {
    return user.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value
      == PrincipalClaimTypes.ServerServiceAccount;
  }
}