using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Services;

public interface IEnvironmentHelper
{
    bool IsDebug { get; }
    bool IsMobileDevice { get; }
    bool IsWindows { get; }
    SystemPlatform Platform { get; }
    RuntimeId Runtime { get; }
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

    public bool IsMobileDevice => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
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

    public RuntimeId Runtime
    {
        get
        {
            return RuntimeInformation.RuntimeIdentifier switch
            {
                "win-x64" => RuntimeId.WinX64,
                "win-x86" => RuntimeId.WinX86,
                "linux-x64" => RuntimeId.LinuxX64,
                "osx-x64" => RuntimeId.OsxX64,
                "osx-arm64" => RuntimeId.OsxArm64,
                _ => throw new PlatformNotSupportedException()
            };
        }
    }

    public string StartupDirectory =>
        Path.GetDirectoryName(StartupExePath) ??
        throw new DirectoryNotFoundException("Unable to determine startup directory.");

    public string StartupExePath { get; } = Environment.ProcessPath ?? Environment.GetCommandLineArgs().First();
}