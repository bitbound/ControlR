using ControlR.Agent.Common.Services.Linux;

namespace ControlR.Agent.Common.Startup;

internal static class PathConstants
{
  public static string GetAppSettingsPath(string? instanceId)
  {
    var dir = GetSettingsDirectory(instanceId);
    return Path.Combine(dir, "appsettings.json");
  }

  public static string GetLogsPath(string? instanceId)
  {
    var settingsDir = GetSettingsDirectory(instanceId);
    return Path.Combine(settingsDir, "Logs", "ControlR.Agent", "LogFile.log");
  }

  private static string GetSettingsDirectory(string? instanceId)
  {
    if (OperatingSystem.IsWindows())
    {
      var settingsDir = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
          "ControlR");

      if (SystemEnvironment.Instance.IsDebug)
      {
        settingsDir = Path.Combine(settingsDir, "Debug");
      }
      if (!string.IsNullOrWhiteSpace(instanceId))
      {
        settingsDir = Path.Combine(settingsDir, instanceId);
      }

      return Directory.CreateDirectory(settingsDir).FullName;
    }

    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {

      var settingsDir = ElevationCheckerLinux.Instance.IsElevated()
        ? "/etc/controlr"
        : "~/.controlr";

      if (!string.IsNullOrWhiteSpace(instanceId))
      {
        settingsDir = Path.Combine(settingsDir, instanceId);
      }
      return Directory.CreateDirectory(settingsDir).FullName;
    }

    throw new PlatformNotSupportedException();
  }
}