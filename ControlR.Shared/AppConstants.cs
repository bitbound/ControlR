using ControlR.Shared.Enums;
using ControlR.Shared.Services;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ControlR.Shared;

public static partial class AppConstants
{
    private const string DevServerUri = "http://localhost:5120";
    private const string ProdServerUri = "https://app.controlr.app";

    public static string ExternalDownloadsUri => "https://controlr.app";

    public static string RemoteControlFileName
    {
        get
        {
            return EnvironmentHelper.Instance.Platform switch
            {
                SystemPlatform.Windows => "controlr-streamer.exe",
                SystemPlatform.Android => "controlr-streamer.exe",
                SystemPlatform.Linux => "controlr-streamer",
                SystemPlatform.MacOS => throw new PlatformNotSupportedException(),
                SystemPlatform.MacCatalyst => throw new PlatformNotSupportedException(),
                _ => throw new PlatformNotSupportedException(),
            };
        }
    }

    public static string RemoteControlZipFileName
    {
        get
        {
            return EnvironmentHelper.Instance.Platform switch
            {
                SystemPlatform.Windows => "controlr-streamer-win.zip",
                _ => throw new PlatformNotSupportedException(),
            };
        }
    }

    public static string ServerUri
    {
        get
        {
            if (OperatingSystem.IsWindows() && Debugger.IsAttached)
            {
                return DevServerUri;
            }
            return ProdServerUri;
        }
    }

    public static string ViewerFileName
    {
        get
        {
            return EnvironmentHelper.Instance.Platform switch
            {
                SystemPlatform.Windows => "ControlR.Viewer.msix",
                SystemPlatform.Android => "ControlR.Viewer.apk",
                _ => throw new PlatformNotSupportedException(),
            };
        }
    }

    public static string GetAgentFileDownloadPath(RuntimeId runtime)
    {
        return runtime switch
        {
            RuntimeId.WinX64 => "/downloads/win-x64/ControlR.Agent.exe",
            RuntimeId.WinX86 => "/downloads/win-x86/ControlR.Agent.exe",
            RuntimeId.LinuxX64 => "/downloads/linux-x64/ControlR.Agent",
            RuntimeId.OsxX64 => "/downloads/osx-x64/ControlR.Agent",
            RuntimeId.OsxArm64 => "/downloads/osx-arm64/ControlR.Agent",
            _ => throw new PlatformNotSupportedException()
        };
    }

    public static string GetAgentFileName(SystemPlatform platform)
    {
        return platform switch
            {
                SystemPlatform.Windows => "ControlR.Agent.exe",
                SystemPlatform.Android => "ControlR.Agent.exe",
                SystemPlatform.Linux => "ControlR.Agent",
                SystemPlatform.MacOS => "ControlR.Agent",
                _ => throw new PlatformNotSupportedException(),
            };
    }
    public static string GetStreamerFileDownloadPath(RuntimeId runtime)
    {
        return runtime switch
        {
            RuntimeId.WinX64 or RuntimeId.WinX86 => "/downloads/win-x64/controlr-streamer-win.zip",
            _ => throw new PlatformNotSupportedException()
        };
    }

    [GeneratedRegex("[^A-Za-z0-9_-]")]
    public static partial Regex UsernameValidator();
}