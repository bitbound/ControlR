using ControlR.Shared.Enums;

namespace ControlR.Shared.Services;

public interface IEnvironmentHelper
{
    bool IsDebug { get; }
    bool IsWindows { get; }
    SystemPlatform Platform { get; }
    string StartupDirectory { get; }
    string StartupExePath { get; }
}

internal class EnvironmentHelper : IEnvironmentHelper
{
    public static EnvironmentHelper Instance { get; } = new();

    public bool IsDebug
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public bool IsWindows => OperatingSystem.IsWindows();

    public SystemPlatform Platform
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return SystemPlatform.Windows;
            }
            else if (OperatingSystem.IsLinux())
            {
                return SystemPlatform.Linux;
            }
            else if (OperatingSystem.IsMacOS())
            {
                return SystemPlatform.MacOS;
            }
            else if (OperatingSystem.IsMacCatalyst())
            {
                return SystemPlatform.MacCatalyst;
            }
            else if (OperatingSystem.IsAndroid())
            {
                return SystemPlatform.Android;
            }
            else if (OperatingSystem.IsIOS())
            {
                return SystemPlatform.IOS;
            }
            else if (OperatingSystem.IsBrowser())
            {
                return SystemPlatform.Browser;
            }
            else
            {
                return SystemPlatform.Unknown;
            }
        }
    }

    public string StartupDirectory =>
        Path.GetDirectoryName(StartupExePath) ??
        throw new DirectoryNotFoundException("Unable to determine startup directory.");

    public string StartupExePath { get; } = Environment.ProcessPath ?? Environment.GetCommandLineArgs().First();
}