using ControlR.Libraries.DataRedaction;

namespace ControlR.Web.Server.Options;

/// <summary>
/// Configuration options for bootstrapping an initial admin user on first startup.
/// </summary>
public class BootstrapOptions
{
  /// <summary>
  /// The configuration section key for BootstrapOptions in appsettings.json.
  /// </summary>
  public const string SectionKey = "Bootstrap";

  /// <summary>
  /// The admin user email address. Used as both the username and email.
  /// </summary>
  public string? AdminEmail { get; init; }
  /// <summary>
  /// The admin user password. Must meet the configured password policy.
  /// </summary>
  [ProtectedDataClassification]
  public string? AdminPassword { get; init; }
  /// <summary>
  /// The pre-shared secret for bootstrapping a Personal Access Token (PAT).
  /// The resulting token format is <c>{hex-guid}:{AdminPatSecret}</c>.
  /// Must be supplied alongside <see cref="AdminPatTokenId"/> for the PAT to be created.
  /// </summary>
  [ProtectedDataClassification]
  public string? AdminPatSecret { get; init; }
  /// <summary>
  /// The pre-assigned GUID for the bootstrap admin PAT.
  /// The resulting token format is <c>{hex-guid}:{AdminPatSecret}</c>.
  /// Must be supplied alongside <see cref="AdminPatSecret"/> for the PAT to be created.
  /// </summary>
  [ProtectedDataClassification]
  public string? AdminPatTokenId { get; init; }
  /// <summary>
  /// Human-readable description for the bootstrapped server service account.
  /// </summary>
  public string? ServerServiceAccountDescription { get; init; }
  /// <summary>
  /// Display name for the bootstrapped server-scoped service account. When set alongside
  /// <see cref="ServerServiceAccountTokenId"/> and <see cref="ServerServiceAccountTokenSecret"/>,
  /// a <see cref="ServiceAccountKind.Server"/> service account and its initial credential are created
  /// on first startup. Omit all three to skip bootstrapping the server service account.
  /// </summary>
  public string? ServerServiceAccountName { get; init; }
  /// <summary>
  /// Pre-assigned deterministic GUID for the bootstrap server service account credential.
  /// The resulting <c>x-api-key</c> header is <c>{hex-guid}:{ServerServiceAccountTokenSecret}</c>.
  /// </summary>
  [ProtectedDataClassification]
  public string? ServerServiceAccountTokenId { get; init; }
  /// <summary>
  /// Pre-shared secret for the bootstrap server service account credential.
  /// The resulting <c>x-api-key</c> header is <c>{hex-guid}:{ServerServiceAccountTokenSecret}</c>.
  /// </summary>
  [ProtectedDataClassification]
  public string? ServerServiceAccountTokenSecret { get; init; }
}
