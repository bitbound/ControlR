using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Services;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ControlR.Libraries.Shared;

public static partial class AppConstants
{
    public static Uri DevServerUri { get; } = new Uri("http://localhost:5120");
    public static Uri ProdServerUri { get; } = new Uri("https://app.controlr.app");

    public static string ExternalDownloadsUri => "https://controlr.app";

    public static string StreamerFileName
    {
        get
        {
            return EnvironmentHelper.Instance.Platform switch
            {
                SystemPlatform.Windows => "ControlR.Streamer.exe",
                SystemPlatform.Linux => "ControlR.Streamer",
                SystemPlatform.Android => throw new PlatformNotSupportedException(),
                SystemPlatform.MacOS => throw new PlatformNotSupportedException(),
                SystemPlatform.MacCatalyst => throw new PlatformNotSupportedException(),
                _ => throw new PlatformNotSupportedException(),
            };
        }
    }

    public static string StreamerZipFileName
    {
        get
        {
            return EnvironmentHelper.Instance.Platform switch
            {
                SystemPlatform.Windows => "ControlR.Streamer.zip",
                _ => throw new PlatformNotSupportedException(),
            };
        }
    }

    public static Uri ServerUri
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
            RuntimeId.WinX64 or RuntimeId.WinX86 => "/downloads/win-x86/ControlR.Streamer.zip",
            _ => throw new PlatformNotSupportedException()
        };
    }

    [GeneratedRegex("[^A-Za-z0-9_-]")]
    public static partial Regex UsernameValidator();

    [GeneratedRegex("[^A-Za-z0-9_@-]")]
    public static partial Regex PublicKeyLabelValidator();
}