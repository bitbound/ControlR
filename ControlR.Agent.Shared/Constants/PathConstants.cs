using ControlR.Libraries.Branding;

namespace ControlR.Agent.Shared.Constants;

public static class PathConstants
{
  public const string MacApplicationsDirectory = "/Applications";
  public static string MacDesktopExecutableRelativePath => $"Contents/MacOS/{BrandingConstants.DesktopClientBaseName}";

  public static string GetMacAppBundleName(string? instanceId)
  {
    return string.IsNullOrWhiteSpace(instanceId)
      ? $"{BrandingConstants.MacAppBundleBaseName}.app"
      : $"{BrandingConstants.MacAppBundleBaseName}.{instanceId}.app";
  }

  public static string GetMacInstalledAppPath(string? instanceId)
  {
    var appBundleName = GetMacAppBundleName(instanceId);

    return $"{MacApplicationsDirectory}/{appBundleName}";
  }
}