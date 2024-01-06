using ControlR.Shared.Services;

namespace ControlR.Agent.Startup;

internal static class LoggingConstants
{
    public static string LogPath => Path.Combine(LogsFolderPath, "ControlR.Agent", $"LogFile_{DateTime.Now:yyyy-MM-dd}.log");

    public static string LogsFolderPath
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
}