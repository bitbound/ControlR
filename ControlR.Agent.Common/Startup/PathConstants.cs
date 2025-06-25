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
    string? logsDir;

    if (OperatingSystem.IsWindows())
    {
      logsDir = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
          "ControlR",
          "Logs");
    }
    else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
      logsDir = ElevationCheckerLinux.Instance.IsElevated()
        ? "/var/log/controlr"
        : "~/.controlr/logs";
    }
    else
    {
      throw new PlatformNotSupportedException();
    }

    logsDir = AppendSubDirectories(logsDir, instanceId);
    return Path.Combine(logsDir, "ControlR.Agent", "LogFile.log");
  }

  private static string GetSettingsDirectory(string? instanceId)
  {
    if (OperatingSystem.IsWindows())
    {
      var rootDir = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
          "ControlR");

      return AppendSubDirectories(rootDir, instanceId);
    }

    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {

      var rootDir = ElevationCheckerLinux.Instance.IsElevated()
        ? "/etc/controlr"
        : "~/.controlr";

      return AppendSubDirectories(rootDir, instanceId);
    }

    throw new PlatformNotSupportedException();
  }

  private static string AppendSubDirectories(string rootDir, string? instanceId)
  {
    if (OperatingSystem.IsWindows())
    {
      if (SystemEnvironment.Instance.IsDebug)
      {
        rootDir = Path.Combine(rootDir, "Debug");
      }
      if (!string.IsNullOrWhiteSpace(instanceId))
      {
        rootDir = Path.Combine(rootDir, instanceId);
      }

      return Directory.CreateDirectory(rootDir).FullName;
    }

    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
      if (!string.IsNullOrWhiteSpace(instanceId))
      {
        rootDir = Path.Combine(rootDir, instanceId);
      }
      return Directory.CreateDirectory(rootDir).FullName;
    }

    throw new PlatformNotSupportedException();
  }
}