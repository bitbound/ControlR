using System.Text.RegularExpressions;

namespace ControlR.Libraries.Branding;

/// <summary>
/// Every name a brand derives from its own name. Code that has to address an install of a different
/// brand asks for that brand's names instead of deriving them itself, because hand-deriving only part
/// of the set is how a name gets missed.
/// </summary>
public sealed partial class BrandNames(string brandName)
{
  /// <summary>
  /// The names of the brand this assembly was compiled as.
  /// </summary>
  public static BrandNames Current { get; } = new(BrandingConstants.BrandName);

  public string AgentBaseName => $"{BrandKey}.Agent";
  public string BrandKey { get; } = ResolveBrandKey(brandName);
  public string BundleHashFileName => $".{UnixBrandKey}-bundle.sha256";
  public string BundleZipBaseName => $"{BrandKey}.Agent.bundle";
  public string DesktopClientBaseName => $"{BrandKey}.DesktopClient";
  public string InstallerBaseName => $"{BrandKey}.Agent.Installer";
  public string IpcPipeBaseName => $"{UnixBrandKey}-ipc-server";
  public string LinuxAgentServiceName => $"{UnixBrandKey}.agent.service";
  public string LinuxDesktopServiceName => $"{UnixBrandKey}.desktop.service";
  public string MacAppBundleBaseName => BrandKey;
  public string MacBundleStateDirectoryName => BrandKey;
  public string MacServicePrefix => $"app.{UnixBrandKey}";
  public string RepairStageDirectoryPrefix => $".{UnixBrandKey}-desktop-repair-";
  public string UnixBrandKey => BrandKey.ToLowerInvariant();
  public string UnixConfigDirectoryName => UnixBrandKey;
  public string UnixHiddenDirectoryName => $".{UnixBrandKey}";
  public string UnixLogDirectoryName => UnixBrandKey;
  public string UpdaterTempDirectoryName => $"{BrandKey}_Update";
  public string WindowsLogDirectoryName => BrandKey;
  public string WindowsServiceBaseName => $"{BrandKey}.Agent";
  public string WindowsUninstallRegistryKeyName => BrandKey;

  /// <summary>
  /// Whether two brand names address the same install. Every directory, service, and registry name
  /// derives from the key rather than the raw name, so a rebrand that only changes punctuation, such
  /// as "Acme Remote" to "Acme-Remote", still describes one install. Code that decides whether an
  /// install has to move has to compare this way, or it will move an install onto itself and then
  /// remove what it wrote.
  /// </summary>
  public static bool AreSameInstall(string? firstBrandName, string? secondBrandName)
  {
    return string.Equals(
      SanitizeBrandKey(firstBrandName ?? string.Empty),
      SanitizeBrandKey(secondBrandName ?? string.Empty),
      StringComparison.Ordinal);
  }

  /// <summary>
  /// Reduces a brand name to the filesystem-safe form used for directory, service, and registry names.
  /// The formula has to stay identical to the one the build script uses to name the files it ships,
  /// or this build's names refer to files that were never written. Two names that share a key address
  /// the same install, so compare keys rather than names.
  /// </summary>
  public static string SanitizeBrandKey(string brandName)
  {
    if (string.IsNullOrWhiteSpace(brandName))
    {
      return string.Empty;
    }

    return BrandNameSanitizer().Replace(brandName, "_");
  }

  [GeneratedRegex(@"[^a-zA-Z0-9]")]
  private static partial Regex BrandNameSanitizer();

  private static string ResolveBrandKey(string brandName)
  {
    if (string.IsNullOrWhiteSpace(brandName))
    {
      throw new ArgumentException("Brand name is required.", nameof(brandName));
    }

    return SanitizeBrandKey(brandName);
  }
}
