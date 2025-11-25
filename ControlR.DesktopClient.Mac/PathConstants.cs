using ControlR.Libraries.NativeInterop.Unix;

namespace ControlR.DesktopClient.Mac;

public static class PathConstants
{
  public static string GetLogsPath(string? instanceId)
  {
    var isRoot = Libc.Geteuid() == 0;
    var rootDir = isRoot
       ? "/var/log/controlr"
       : $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.controlr";

    rootDir = AppendInstanceId(rootDir, instanceId);
    var logsDir = isRoot ? rootDir : Path.Combine(rootDir, "logs");
    return Path.Combine(logsDir, "ControlR.DesktopClient", "LogFile.log");
  }

  private static string AppendInstanceId(string rootDir, string? instanceId)
  {
    if (!string.IsNullOrWhiteSpace(instanceId))
    {
      rootDir = Path.Combine(rootDir, instanceId);
    }
    return Directory.CreateDirectory(rootDir).FullName;
  }

}