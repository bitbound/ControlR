using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.Extensions;

/// <summary>
/// Resolves the caller's tenant, and tells the user when there is not one to resolve.
/// </summary>
/// <remarks>
/// Tenant-scoped V1 requests all carry a required <c>tenantId</c>, so a client that cannot resolve one
/// from its own claims must not send the request at all. Reading the claim here is a fail-fast
/// convenience and a source of the required parameter, never an authorization decision. The server
/// re-validates the tenant against the caller's claims and is the trust boundary.
/// </remarks>
public static class TenantResolutionExtensions
{
  /// <summary>
  /// The single copy of the message shown when a tenant cannot be resolved. Call sites reach it
  /// through one of the helpers below instead of restating the text.
  /// </summary>
  public const string NoTenantMessage = "No tenant is associated with the signed-in user.";

  /// <summary>
  /// Reads the tenant from the current authentication state without reporting a failure, for callers
  /// that already have their own fallback.
  /// </summary>
  public static async Task<Guid?> GetTenantIdAsync(
    this AuthenticationStateProvider authState)
  {
    var state = await authState.GetAuthenticationStateAsync();
    return state.User.TryGetTenantId(out var tenantId) ? tenantId : null;
  }

  /// <summary>
  /// Reads the tenant from the current authentication state, notifying
  /// <paramref name="snackbar"/> when there is none.
  /// </summary>
  public static async Task<Guid?> GetTenantIdAsync(
    this AuthenticationStateProvider authState,
    ISnackbar snackbar)
  {
    var state = await authState.GetAuthenticationStateAsync();
    return state.User.TryGetTenantId(snackbar, out var tenantId) ? tenantId : null;
  }

  /// <summary>
  /// Resolves the tenant from <paramref name="user"/>, notifying <paramref name="snackbar"/> when the
  /// claim is missing or unparseable so a call site can bail in a single line.
  /// </summary>
  public static bool TryGetTenantId(
    this ClaimsPrincipal user,
    ISnackbar snackbar,
    out Guid tenantId)
  {
    if (user.TryGetTenantId(out tenantId))
    {
      return true;
    }

    snackbar.Add(NoTenantMessage, Severity.Error);
    tenantId = Guid.Empty;
    return false;
  }
}
