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
    if (OperatingSystem.IsWindows())
    {
      var logsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ControlR");

      logsDir = AppendSubDirectories(logsDir, instanceId);
      return Path.Combine(logsDir, "Logs", "ControlR.Agent", "LogFile.log");
    }

    // ReSharper disable once InvertIf
    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
      var isElevated = ElevationCheckerLinux.Instance.IsElevated();
      var rootDir = isElevated
        ? "/var/log/controlr"
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".controlr");

      rootDir = AppendSubDirectories(rootDir, instanceId);
      var logsDir = isElevated ? rootDir : Path.Combine(rootDir, "logs");
      return Path.Combine(logsDir, "ControlR.Agent", "LogFile.log");
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

    // ReSharper disable once InvertIf
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

  private static string GetSettingsDirectory(string? instanceId)
  {
    if (OperatingSystem.IsWindows())
    {
      var rootDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ControlR");

      return AppendSubDirectories(rootDir, instanceId);
    }

    // ReSharper disable once InvertIf
    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
      var rootDir = ElevationCheckerLinux.Instance.IsElevated()
        ? "/etc/controlr"
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".controlr");

      return AppendSubDirectories(rootDir, instanceId);
    }

    throw new PlatformNotSupportedException();
  }
}