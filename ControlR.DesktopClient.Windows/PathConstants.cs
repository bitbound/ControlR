using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services;

namespace ControlR.DesktopClient.Windows;

public static class PathConstants
{
  public static string GetLogsPath(string? instanceId)
  {
    var logsDir = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
          BrandingConstants.WindowsInstallDirectoryName);
    logsDir = AppendSubDirectories(logsDir, instanceId);
    return Path.Combine(logsDir, "Logs", BrandingConstants.DesktopClientBaseName, "LogFile.log");
  }

  private static string AppendSubDirectories(string rootDir, string? instanceId)
  {
    if (SystemEnvironment.Instance.IsDebug)
    {
      rootDir = Path.Combine(rootDir, "Debug");
    }

    return  Path.Combine(rootDir, GetEffectiveInstanceId(instanceId));
  }

  private static string GetEffectiveInstanceId(string? instanceId)
  {
    return string.IsNullOrWhiteSpace(instanceId)
      ? AppConstants.DefaultInstallDirectoryName
      : instanceId;
  }
}