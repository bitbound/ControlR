using System.Security.Claims;
using ControlR.Web.Server.Authn;

namespace ControlR.Web.Server.Extensions;

/// <summary>
/// Extension methods for reading canonical <c>controlr:*</c> claims from a
/// <see cref="ClaimsPrincipal"/>. These claims are emitted by all authentication
/// handlers (PAT, logon token, service account credential, cookie, interactive bearer).
/// </summary>
public static class ServerPrincipalExtensions
{

  /// <summary>
  /// True for server principals, or when the caller's tenant matches
  /// <paramref name="resourceTenantId"/>. Use it when the resource tenant is derived from the
  /// resource itself (e.g. <c>device.TenantId</c>), which is where it adds real defense. Do not
  /// call it with a tenant id that <see cref="TryResolveTenantId"/> just resolved for the same
  /// caller. Success there already guarantees this check passes, so the re-check is redundant.
  /// </summary>
  public static bool CanAccessTenant(this ClaimsPrincipal user, Guid resourceTenantId)
  {
    if (user.IsServerPrincipal())
    {
      return true;
    }

    return user.TryGetTenantId(out var callerTenantId) && callerTenantId == resourceTenantId;
  }

  /// <summary>
  /// Maps the authenticated principal's <c>controlr:principal:type</c> claim to the
  /// corresponding <see cref="InstallerKeyCreatorKind"/>. Defaults to <see cref="InstallerKeyCreatorKind.User"/>
  /// when the claim is absent or unrecognized.
  /// </summary>
  public static InstallerKeyCreatorKind GetCreatorKind(this ClaimsPrincipal user)
  {
    return user.GetPrincipalType() switch
    {
      PrincipalClaimValues.ServerServiceAccount => InstallerKeyCreatorKind.ServerServiceAccount,
      PrincipalClaimValues.TenantServiceAccount => InstallerKeyCreatorKind.TenantServiceAccount,
      _ => InstallerKeyCreatorKind.User,
    };
  }

  /// <summary>
  /// Gets the principal type value from the <c>controlr:principal:type</c> claim, or null.
  /// </summary>
  public static string? GetPrincipalType(this ClaimsPrincipal user)
  {
    return user.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value;
  }

  /// <summary>
  /// Returns true when the principal is a server-scoped service account.
  /// </summary>
  public static bool IsServerPrincipal(this ClaimsPrincipal user)
  {
    return user.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value
      == PrincipalClaimValues.ServerServiceAccount;
  }

  /// <summary>
  /// Tries to extract the credential id from the <c>controlr:credential:id</c> claim.
  /// </summary>
  public static bool TryGetCredentialId(this ClaimsPrincipal user, out Guid credentialId)
  {
    var claim = user.FindFirst(PrincipalClaimTypes.CredentialId);
    return Guid.TryParse(claim?.Value, out credentialId);
  }

  /// <summary>
  /// Tries to extract the stable principal id (user or service account) from the
  /// <c>controlr:principal:id</c> claim emitted by all authentication handlers.
  /// </summary>
  public static bool TryGetPrincipalId(this ClaimsPrincipal user, out Guid principalId)
  {
    var claim = user.FindFirst(PrincipalClaimTypes.PrincipalId);
    return Guid.TryParse(claim?.Value, out principalId);
  }

  /// <summary>
  /// Trusts <paramref name="requestTenantId"/> for server principals; otherwise requires the
  /// caller's tenant claim to match it (the request body is never the source of truth).
  /// </summary>
  public static bool TryResolveTenantId(
    this ClaimsPrincipal user,
    Guid requestTenantId,
    out Guid tenantId)
  {
    if (user.IsServerPrincipal())
    {
      tenantId = requestTenantId;
      return true;
    }

    if (!user.TryGetTenantId(out var callerTenantId) || callerTenantId != requestTenantId)
    {
      tenantId = Guid.Empty;
      return false;
    }

    tenantId = callerTenantId;
    return true;
  }
}
