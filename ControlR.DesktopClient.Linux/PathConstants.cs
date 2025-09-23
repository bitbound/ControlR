using ControlR.Libraries.NativeInterop.Unix;

namespace ControlR.DesktopClient.Linux;

public static class PathConstants
{
  public static string GetAppSettingsPath(string? instanceId)
  {
    var dir = GetSettingsDirectory(instanceId);
    return Path.Combine(dir, "appsettings.json");
  }

  public static string GetLogsPath(string? instanceId)
  {
    var logsDir = Libc.Geteuid() == 0
       ? "/var/log/controlr"
       : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".controlr", "logs");

    logsDir = AppendSubDirectories(logsDir, instanceId);
    return Path.Combine(logsDir, "ControlR.DesktopClient", "LogFile.log");
  }

  private static string GetSettingsDirectory(string? instanceId)
  {
    var rootDir = Libc.Geteuid() == 0
      ? "/etc/controlr"
      : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".controlr");

    return AppendSubDirectories(rootDir, instanceId);
  }

  private static string AppendSubDirectories(string rootDir, string? instanceId)
  {
    if (!string.IsNullOrWhiteSpace(instanceId))
    {
      rootDir = Path.Combine(rootDir, instanceId);
    }
    return Directory.CreateDirectory(rootDir).FullName;
  }
}