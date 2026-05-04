namespace ControlR.Agent.Shared.Constants;

public static class PathConstants
{
  public const string MacApplicationsDirectory = "/Applications";
  public static string MacDesktopExecutableRelativePath => "Contents/MacOS/ControlR.DesktopClient";

  public static string GetMacAppBundleName(string? instanceId)
  {
    return string.IsNullOrWhiteSpace(instanceId)
      ? "ControlR.app"
      : $"ControlR.{instanceId}.app";
  }

  public static string GetMacInstalledAppPath(string? instanceId)
  {
    var appBundleName = GetMacAppBundleName(instanceId);

    return $"{MacApplicationsDirectory}/{appBundleName}";
  }
}