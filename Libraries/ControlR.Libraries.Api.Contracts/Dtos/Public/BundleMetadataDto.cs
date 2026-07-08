using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.Public;

/// <summary>
/// Metadata for a versioned bundle containing the agent and desktop client for a specific runtime.
/// </summary>
public class BundleMetadataDto
{
  /// <summary>
  /// The download URL for the bundle ZIP file containing both DesktopClient and Agent.
  /// </summary>
  public required string BundleDownloadUrl { get; set; }
  /// <summary>
  /// SHA-256 hash of the bundle ZIP file for integrity validation.
  /// </summary>
  public required string BundleSha256 { get; set; }
  /// <summary>
  /// The download URL for the bootstrap installer that downloads and installs the bundle.
  /// </summary>
  public required string InstallerDownloadUrl { get; set; }
  /// <summary>
  /// SHA-256 hash of the installer for integrity validation.
  /// </summary>
  public required string InstallerSha256 { get; set; }
  /// <summary>
  /// The runtime ID this bundle is for (e.g., WinX64, LinuxX64, MacOsArm64).
  /// </summary>
  public required RuntimeId Runtime { get; set; }
  /// <summary>
  /// The version of the agent included in this bundle.
  /// </summary>
  public required Version Version { get; set; }
}