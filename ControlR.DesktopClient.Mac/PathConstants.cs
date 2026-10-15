using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.NativeInterop.Unix;

namespace ControlR.DesktopClient.Mac;

/// <summary>
/// <para>
/// File system path constants used by DesktopClient.
/// </para>
/// <para>
/// NOTE: Each platform has one of these files and is accessed behind preprocessor directives.
/// Displayed references might be incorrect.
/// </para>
/// </summary>
public static class PathConstants
{
  public static string GetLogsPath(string? instanceId)
  {
    var isRoot = Libc.Geteuid() == 0;
    // /var is a link to /private/var on macOS, and the remote-log guard refuses a
    // link anywhere along a log path, so the root path names the real location.
    var rootDir = isRoot
       ? $"/private/var/log/{BrandingConstants.UnixLogDirectoryName}"
       : $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/{BrandingConstants.UnixHiddenDirectoryName}";

    rootDir = AppendInstanceId(rootDir, instanceId);
    var logsDir = isRoot ? rootDir : Path.Combine(rootDir, "logs");
    return Path.Combine(logsDir, BrandingConstants.DesktopClientBaseName, "LogFile.log");
  }

  public static string GetSettingsPath(string? instanceId)
  {
    var rootDir = Libc.Geteuid() == 0
      ? $"/etc/{BrandingConstants.UnixConfigDirectoryName}"
      : $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/{BrandingConstants.UnixHiddenDirectoryName}";

    return Path.Combine(AppendInstanceId(rootDir, instanceId), "settings.json");
  }

  private static string AppendInstanceId(string rootDir, string? instanceId)
  {
    return Path.Combine(rootDir, GetEffectiveInstanceId(instanceId));
  }

  private static string GetEffectiveInstanceId(string? instanceId)
  {
    return string.IsNullOrWhiteSpace(instanceId)
      ? AppConstants.DefaultInstanceId
      : instanceId;
  }

}