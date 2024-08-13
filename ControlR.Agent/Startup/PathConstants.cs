using ControlR.Libraries.Shared.Services;

namespace ControlR.Agent.Startup;

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
        return Path.Combine(settingsDir, "Logs", "LogFile.log");
    }
    public static string GetRuntimeSettingsFilePath(string? instanceId)
    {
        var dir = GetSettingsDirectory(instanceId);
        return Path.Combine(dir, "runtime-settings.json");
    }

    private static string GetSettingsDirectory(string? instanceId)
    {
        if (OperatingSystem.IsWindows())
        {
            var settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ControlR");

            if (EnvironmentHelper.Instance.IsDebug)
            {
                settingsDir = Path.Combine(settingsDir, "Debug");
            }
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                settingsDir = Path.Combine(settingsDir, instanceId);
            }

            settingsDir = Path.Combine(settingsDir, "ControlR.Agent");

            return Directory.CreateDirectory(settingsDir).FullName;
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var settingsDir = "/etc/controlr";
            if (EnvironmentHelper.Instance.IsDebug)
            {
                settingsDir += "/debug";
            }
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                settingsDir = Path.Combine(settingsDir, instanceId);
            }
            return Directory.CreateDirectory(settingsDir).FullName;
        }

        throw new PlatformNotSupportedException();
    }
}