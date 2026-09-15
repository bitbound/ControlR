using System.Security.Claims;

namespace ControlR.Web.Client.Extensions;

public static class ClaimsPrincipalExtensions
{
  /// <summary>
  /// The single copy of the message shown when a tenant cannot be resolved. Call sites reach it
  /// through one of the helpers below instead of restating the text.
  /// </summary>
  public const string NoTenantMessage = "No tenant is associated with the signed-in user.";

  /// <summary>
  /// Returns true when the server evaluated the named client policy against its canonical
  /// (tenant/server) resource while producing the current auth snapshot and the decision was
  /// allowed. Client policy grants are <see cref="PermissionPolicies.ClientPolicyClaimType"/>
  /// claims whose value is the policy name. This deliberately does not match resource-scoped
  /// permission names, which are never emitted as global claims.
  /// </summary>
  public static bool HasClientPolicy(this ClaimsPrincipal user, string policyName)
  {
    if (!user.IsAuthenticated())
    {
      return false;
    }

    return user.HasClaim(PermissionPolicies.ClientPolicyClaimType, policyName);
  }

  public static bool IsAuthenticated(this ClaimsPrincipal user)
  {
    return user.Identity?.IsAuthenticated ?? false;
  }

  public static bool TryGetTenantId(
    this ClaimsPrincipal user,
    out Guid tenantId)
  {
    if (!user.IsAuthenticated())
    {
      tenantId = Guid.Empty;
      return false;
    }

    var tenantClaim = user.FindFirst(UserClaimTypes.TenantId);
    return Guid.TryParse(tenantClaim?.Value, out tenantId);
  }

  /// <summary>
  /// Resolves the tenant from <paramref name="user"/>, notifying <paramref name="snackbar"/> when the
  /// claim is missing or unparseable so a call site can bail in a single line.
  /// </summary>
  /// <remarks>
  /// Tenant-scoped V1 requests all carry a required <c>tenantId</c>, so a client that cannot resolve one
  /// from its own claims must not send the request at all. Reading the claim here is a fail-fast
  /// convenience and a source of the required parameter, never an authorization decision. The server
  /// re-validates the tenant against the caller's claims and is the trust boundary.
  /// </remarks>
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

  public static bool TryGetUserId(
    this ClaimsPrincipal user,
    out Guid userId)
  {
    if (!user.IsAuthenticated())
    {
      userId = Guid.Empty;
      return false;
    }

    var userIdClaim = user.FindFirst(UserClaimTypes.UserId);
    return Guid.TryParse(userIdClaim?.Value, out userId);
  }
}