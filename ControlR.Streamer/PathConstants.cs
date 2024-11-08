namespace ControlR.Streamer;

internal static class PathConstants
{
    public static string GetAppSettingsPath(Uri originUri)
    {
        var dir = GetSettingsDirectory(originUri);
        return Path.Combine(dir, "appsettings.json");
    }

    public static string GetLogsPath(Uri originUri)
    {
        var settingsDir = GetSettingsDirectory(originUri);
        return Path.Combine(settingsDir, "Logs", "ControlR.Streamer", "LogFile.log");
    }

    private static string GetSettingsDirectory(Uri originUri)
    {
        var settingsDir = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
              "ControlR");

        if (SystemEnvironment.Instance.IsDebug)
        {
            settingsDir = Path.Combine(settingsDir, "Debug");
        }

        settingsDir = Path.Combine(settingsDir, originUri.Host);

        return Directory.CreateDirectory(settingsDir).FullName;
    }
}