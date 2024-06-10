using ControlR.Libraries.Shared.Services;

namespace ControlR.Agent.Startup;

internal static class PathConstants
{
    private static string LogsFolderPath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var logsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ControlR",
                    "Logs");

                if (EnvironmentHelper.Instance.IsDebug)
                {
                    logsPath += "_Debug";
                }
                return logsPath;
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                if (EnvironmentHelper.Instance.IsDebug)
                {
                    return "/var/log/ControlR_debug";
                }
                return "/var/log/ControlR";
            }

            throw new PlatformNotSupportedException();
        }
    }

    public static string GetAppSettingsPath(string? instanceId)
    {
        var dir = GetSettingsDirectory(instanceId);
        return Path.Combine(dir, "appsettings.json");
    }

    public static string GetLogsPath(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return Path.Combine(LogsFolderPath, "ControlR.Agent", "LogFile.log");
        }

        return Path.Combine(LogsFolderPath, "ControlR.Agent", instanceId, $"LogFile.log");
    }
    public static string GetRuntimeSettingsFilePath(string? instanceId)
    {
        var dir = GetSettingsDirectory(instanceId);
        return Path.Combine(dir, "runtime-settings.json");
    }

    public static string GetSettingsDirectory(string? instanceId)
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