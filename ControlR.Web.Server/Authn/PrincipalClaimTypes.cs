namespace ControlR.Web.Server.Authn;

/// <summary>
/// Canonical claim types and principal-type values used by the permission rework.
/// Phase 1 introduces the <c>server-service-account</c> principal type; Phase 2 adds
/// <c>tenant-service-account</c> and user/credential variants.
/// </summary>
public static class PrincipalClaimTypes
{

  /// <summary>The credential id when the principal authenticated via a credential (PAT, logon token, service account credential).</summary>
  public const string CredentialId = "controlr:credential:id";
  /// <summary>The stable id of the principal (AppUser.Id or ServiceAccount.Id).</summary>
  public const string PrincipalId = "controlr:principal:id";
  /// <summary>Identifies the kind of principal (user, server-service-account, tenant-service-account).</summary>
  public const string PrincipalType = "controlr:principal:type";
  /// <summary>Principal-type value for a server-scoped service account.</summary>
  public const string ServerServiceAccount = "server-service-account";
  /// <summary>Authentication method value for a service-account credential.</summary>
  public const string ServiceAccountCredentialMethod = "service-account-credential";
  /// <summary>Principal-type value for a service (userless) logon token.</summary>
  public const string ServiceToken = "service-token";
}