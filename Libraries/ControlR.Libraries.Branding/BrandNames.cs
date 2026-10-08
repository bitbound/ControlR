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
  public string BrandKey => SanitizeBrandKey(BrandName);
  public string BrandName { get; } = ResolveBrandName(brandName);
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
  /// Reduces a brand name to the filesystem-safe form used for directory, service, and registry names.
  /// Two names that share a key address the same install, so compare keys rather than names.
  /// </summary>
  public static string SanitizeBrandKey(string brandName)
  {
    if (string.IsNullOrWhiteSpace(brandName))
    {
      return string.Empty;
    }

    // Surrounding whitespace shows up when a brand name is read from a customization config file.
    return BrandNameSanitizer().Replace(brandName.Trim(), "_");
  }

  [GeneratedRegex(@"[^a-zA-Z0-9]")]
  private static partial Regex BrandNameSanitizer();

  private static string ResolveBrandName(string brandName)
  {
    if (string.IsNullOrWhiteSpace(brandName))
    {
      throw new ArgumentException("Brand name is required.", nameof(brandName));
    }

    return brandName.Trim();
  }
}
