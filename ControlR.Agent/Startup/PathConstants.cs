using ControlR.Shared.Services;

namespace ControlR.Agent.Startup;

internal static class PathConstants
{
    public static string GetLogsPath(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return Path.Combine(LogsFolderPath, "ControlR.Agent", "LogFile.log");
        }

        return Path.Combine(LogsFolderPath, "ControlR.Agent", instanceId, $"LogFile.log");
    }

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
            var dir = Directory.CreateDirectory(settingsDir).FullName;
            return Path.Combine(dir, "appsettings.json");
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
            var dir = Directory.CreateDirectory(settingsDir).FullName;
            return Path.Combine(dir, "appsettings.json");
        }

        throw new PlatformNotSupportedException();
    }
}