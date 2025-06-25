namespace ControlR.Streamer;

internal static class PathConstants
{
  public static string GetLogsPath(string appDataFolder)
  {
    var logsDir = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
          "ControlR",
          "Logs");
    logsDir = AppendSubDirectories(logsDir, appDataFolder);
    return Path.Combine(logsDir, "ControlR.Streamer", "LogFile.log");
  }

  private static string AppendSubDirectories(string rootDir, string? instanceId)
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
}