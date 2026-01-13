using ControlR.Libraries.DataRedaction;

namespace ControlR.Web.Server.Options;

/// <summary>
/// Options for configuring X.509 certificate-based encryption of Data Protection keys at rest.
/// Keys can be protected using a PFX file (with optional password protection).
/// If no options are configured, keys will not be encrypted at rest.
/// </summary>
public class KeyProtectionOptions
{
  public const string SectionKey = "KeyProtectionOptions";

  /// <summary>
  /// The password for a password-protected PFX certificate file.
  /// Leave empty or null if the PFX file has no password.
  /// </summary>
  [ProtectedDataClassification]
  public string? CertificatePassword { get; init; }

  /// <summary>
  /// The file path to a PFX (.pfx) certificate file for key encryption.
  /// Required when <see cref="EncryptKeys"/> is true.
  /// </summary>
  public string? CertificatePath { get; init; }

  /// <summary>
  /// When true, Data Protection keys will be encrypted at rest using the certificate
  /// specified in <see cref="CertificatePath"/>. An exception will be thrown at startup
  /// if the certificate path is not configured or the file does not exist.
  /// </summary>
  public bool EncryptKeys { get; init; }
}
