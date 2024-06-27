namespace ControlR.Streamer;

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

    public static string GetLogsPath(Uri originUri)
    {
        return Path.Combine(LogsFolderPath, "ControlR.Streamer", originUri.Authority, $"LogFile.log");
    }
}