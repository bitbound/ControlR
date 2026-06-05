using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ControlR.Libraries.Branding;

[SuppressMessage("MemberOrder", "BB0001", Justification = "Constants are grouped by category, not by member type.")]
public static partial class BrandingConstants
{
  public const string BrandName = "ControlR";
  public const string Publisher = "Bitbound";

  [GeneratedRegex(@"[^a-zA-Z0-9]")]
  private static partial Regex BrandNameSanitizer();

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

  private static string BrandKey => BrandNameSanitizer().Replace(BrandName, "_");
  private static string UnixBrandKey => BrandKey.ToLowerInvariant();

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
  public static string WebServerAssemblyName => $"{BrandKey}.Web.Server";
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
}
