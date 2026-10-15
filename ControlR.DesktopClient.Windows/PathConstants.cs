using System.Diagnostics;
using ControlR.Libraries.Branding;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services;
using Serilog;

namespace ControlR.DesktopClient.Windows;

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
  private const string SystemUsername = "System";

  public static string GetLogsPath(string? instanceId)
  {
    var logsDir = Path.Combine(GetDesktopClientDirectory(instanceId), "Logs");
    return Path.Combine(logsDir, "LogFile.log");
  }

  public static string GetSettingsPath(string? instanceId)
  {
    return Path.Combine(GetDesktopClientDirectory(instanceId), "settings.json");
  }

  private static string AppendSubDirectories(string rootDir, string? instanceId)
  {
    if (SystemEnvironment.Instance.IsDebug)
    {
      rootDir = Path.Combine(rootDir, "Debug");
    }

    return Path.Combine(rootDir, GetEffectiveInstanceId(instanceId));
  }

  private static string GetDesktopClientDirectory(string? instanceId)
  {
    var rootDir = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
      BrandingConstants.WindowsInstallDirectoryName);

    var sessionDir = AppendSubDirectories(rootDir, instanceId);

    return Path.Combine(sessionDir, GetUsername(), BrandingConstants.DesktopClientDirectoryName);
  }

  private static string GetEffectiveInstanceId(string? instanceId)
  {
    return string.IsNullOrWhiteSpace(instanceId)
      ? AppConstants.DefaultInstanceId
      : instanceId;
  }

  private static string GetUsername()
  {
    try
    {
      var sessionId = (uint)Process.GetCurrentProcess().SessionId;
      var username = Win32Interop.GetUsernameFromSession(sessionId);

      return string.IsNullOrEmpty(username) ? SystemUsername : username;
    }
    catch (Exception ex)
    {
      // This path names the log file Serilog opens, so failing here would stop the client before
      // the real log exists. Same fallback as a machine sitting at the sign-in screen.
      Log.Warning(ex, "Unable to resolve the username for the current session. Falling back to \"{Username}\".", SystemUsername);
      return SystemUsername;
    }
  }
}
