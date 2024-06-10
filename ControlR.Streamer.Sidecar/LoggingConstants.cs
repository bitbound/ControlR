using ControlR.Libraries.Shared.Services;

namespace ControlR.Streamer.Sidecar;

internal static class LoggingConstants
{
    public static string LogPath => Path.Combine(LogsFolderPath, "ControlR.Streamer.Sidecar", $"LogFile.log");

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
                    return "/var/log/controlr_debug";
                }
                return "/var/log/controlr";
            }

            throw new PlatformNotSupportedException();
        }
    }
}