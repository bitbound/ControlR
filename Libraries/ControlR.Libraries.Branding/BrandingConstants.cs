using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ControlR.Libraries.Branding;

[SuppressMessage("MemberOrder", "BB0001", Justification = "Constants are grouped by category, not by member type.")]
/// <summary>
/// Centralized branding constants. These values are replaced by the build script during customized builds.
/// </summary>
public static partial class BrandingConstants
{
  public const string BrandName = "ControlR";
  public const string Publisher = "Bitbound";

  [GeneratedRegex(@"[^a-zA-Z0-9]")]
  private static partial Regex BrandNameSanitizer();

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

    return BrandNameSanitizer().Replace(brandName, "_");
  }

  public const string PrimaryColorDark = "2196F3";
  public const string SecondaryColorDark = "21f3e9";
  public const string TertiaryColorDark = "7b21f3";
  public const string InfoColorDark = "89b4f8";
  public const string SuccessColorDark = "2cb67d";
  public const string WarningColorDark = "facc15";
  public const string ErrorColorDark = "f87171";

  public const string PrimaryColorLight = "2196F3";
  public const string SecondaryColorLight = "008c7a";
  public const string TertiaryColorLight = "7b21f3";
  public const string InfoColorLight = "0d6efd";
  public const string SuccessColorLight = "28a745";
  public const string WarningColorLight = "ffc107";
  public const string ErrorColorLight = "dc3545";

  public static string BrandKey => SanitizeBrandKey(BrandName);
  public static string UnixBrandKey => BrandKey.ToLowerInvariant();

  private static Uri? ParseControlrServerUrl(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
      || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
      throw new InvalidOperationException(
        $"The baked-in ControlR server URL must be an absolute http/https URL. Got: '{value}'.");
    }

    return uri;
  }

  /// <summary>
  /// The server URL baked into a customized build. Null in the default build.
  /// </summary>
  public static Uri? ControlrServerUrl { get; } = ParseControlrServerUrl(null);

  /// <summary>
  /// Hides the Project, Website, and Sponsor links in the built app UI. False in the default build.
  /// </summary>
  public static bool HideSponsorshipInfo { get; } = false;

  /// <summary>
  /// Brand names whose installed agents this build may migrate. Empty in the default build.
  /// Published to agents so they will accept this build as a successor, and compared against this
  /// build's own brand key to decide whether an inbound bundle may replace the running install.
  /// </summary>
  public static string[] PredecessorBrandNames { get; } = [];

  public static string AuthenticatorIssuerName => BrandName;

  public static string WindowsInstallDirectoryName => BrandKey;
  public static string LinuxInstallDirectoryName => BrandKey;
  public static string MacInstallDirectoryName => BrandKey;

  public static string MacAppBundleBaseName => BrandKey;
  public static string MacBundleStateDirectoryName => BrandKey;
  public static string UpdaterTempDirectoryName => $"{BrandKey}_Update";

  public static string AgentBaseName => $"{BrandKey}.Agent";
  public static string DesktopClientBaseName => $"{BrandKey}.DesktopClient";
  public static string InstallerBaseName => $"{BrandKey}.Agent.Installer";
  public static string WebServerAssemblyName => "ControlR.Web.Server";
  public static string BundleZipBaseName => $"{BrandKey}.Agent.bundle";
  public static string DesktopClientDirectoryName => "DesktopClient";

  public static string WindowsLogDirectoryName => BrandKey;
  public static string UnixLogDirectoryName => UnixBrandKey;
  public static string UnixConfigDirectoryName => UnixBrandKey;
  public static string UnixHiddenDirectoryName => $".{UnixBrandKey}";

  public static string WindowsServiceBaseName => $"{BrandKey}.Agent";
  public static string LinuxAgentServiceName => $"{UnixBrandKey}.agent.service";
  public static string LinuxDesktopServiceName => $"{UnixBrandKey}.desktop.service";
  public static string MacServicePrefix => $"app.{UnixBrandKey}";

  public static string WindowsUninstallRegistryKeyName => BrandKey;

  public static string BundleHashFileName => $".{UnixBrandKey}-bundle.sha256";
  public static string RepairStageDirectoryPrefix => $".{UnixBrandKey}-desktop-repair-";

  public static string IpcPipeBaseName => $"{UnixBrandKey}-ipc-server";
}
