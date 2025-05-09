namespace ControlR.Streamer;

internal static class PathConstants
{
    public static string GetAppSettingsPath(string appDataFolder)
    {
        var dir = GetSettingsDirectory(appDataFolder);
        return Path.Combine(dir, "appsettings.json");
    }

    public static string GetLogsPath(string appDataFolder)
    {
        var settingsDir = GetSettingsDirectory(appDataFolder);
        return Path.Combine(settingsDir, "Logs", "ControlR.Streamer", "LogFile.log");
    }

    private static string GetSettingsDirectory(string appDataFolder)
    {
        var settingsDir = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
              "ControlR");

        if (SystemEnvironment.Instance.IsDebug)
        {
            settingsDir = Path.Combine(settingsDir, "Debug");
        }

        settingsDir = Path.Combine(settingsDir, appDataFolder);

        return Directory.CreateDirectory(settingsDir).FullName;
    }
}