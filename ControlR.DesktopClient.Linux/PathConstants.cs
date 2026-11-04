using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.NativeInterop.Unix;

namespace ControlR.DesktopClient.Linux;

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
  /// <summary>
  /// TODO: Temporary migration code for v0.31.x
  /// The settings directory this client used before the publisher became a directory in it.
  /// </summary>
  public static string GetLegacySettingsDirectory(string? instanceId)
  {
    var rootDir = Libc.Geteuid() == 0
      ? $"/etc/{BrandingConstants.UnixConfigDirectoryName}"
      : Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
          BrandingConstants.UnixHiddenDirectoryName);

    return AppendInstanceId(rootDir, instanceId);
  }

  public static string GetLogsPath(string? instanceId)
  {
    var isRoot = Libc.Geteuid() == 0;
    var rootDir = isRoot
       ? $"/var/log/{InstallPathNames.LowercaseUnixDirectoryTail(BrandingConstants.Publisher, BrandingConstants.BrandName)}"
       : Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
          InstallPathNames.UnixHiddenHomeTail(BrandingConstants.Publisher, BrandingConstants.BrandName));

    rootDir = AppendInstanceId(rootDir, instanceId);
    var logsDir = isRoot ? rootDir : Path.Combine(rootDir, "logs");
    return Path.Combine(logsDir, BrandingConstants.DesktopClientBaseName, "LogFile.log");
  }

  public static string GetSettingsPath(string? instanceId)
  {
    return Path.Combine(GetSettingsDirectory(instanceId), "settings.json");
  }

  public static string GetWaylandRemoteDesktopRestoreTokenPath(string? instanceId)
  {
    var dir = GetSettingsDirectory(instanceId);
    return Path.Combine(dir, "wayland-remotedesktop-restore-token");
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

  private static string GetSettingsDirectory(string? instanceId)
  {
    var rootDir = Libc.Geteuid() == 0
      ? $"/etc/{InstallPathNames.LowercaseUnixDirectoryTail(BrandingConstants.Publisher, BrandingConstants.BrandName)}"
      : Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
          InstallPathNames.UnixHiddenHomeTail(BrandingConstants.Publisher, BrandingConstants.BrandName));

    return AppendInstanceId(rootDir, instanceId);
  }
}