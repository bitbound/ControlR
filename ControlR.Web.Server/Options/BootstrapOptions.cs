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
  public string? AdminPatTokenId { get; init; }
}
