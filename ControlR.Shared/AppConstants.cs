using ControlR.Shared.Enums;
using ControlR.Shared.Services;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ControlR.Shared;

public static partial class AppConstants
{
    public const string AgentCertificateThumbprint = "4b6235f1c44ab3a5f29bf40ad85b442269f6ee52";

    public static string AgentFileName
    {
        get
        {
            return EnvironmentHelper.Instance.Platform switch
            {
                SystemPlatform.Windows => "ControlR.Agent.exe",
                SystemPlatform.Linux => "ControlR.Agent",
                SystemPlatform.MacOS => throw new PlatformNotSupportedException(),
                SystemPlatform.MacCatalyst => throw new PlatformNotSupportedException(),
                _ => throw new PlatformNotSupportedException(),
            };
        }
    }

    public static string RemoteControlFileName
    {
        get
        {
            return EnvironmentHelper.Instance.Platform switch
            {
                SystemPlatform.Windows => "controlr-streamer.exe",
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
                SystemPlatform.Linux => "controlr-streamer-linux.zip",
                SystemPlatform.MacOS => throw new PlatformNotSupportedException(),
                SystemPlatform.MacCatalyst => throw new PlatformNotSupportedException(),
                _ => throw new PlatformNotSupportedException(),
            };
        }
    }

    public static string ServerUri
    {
        get
        {
            var envUri = Environment.GetEnvironmentVariable("ControlRServerUri");
            if (Uri.TryCreate(envUri, UriKind.Absolute, out _))
            {
                return envUri;
            }

            if (OperatingSystem.IsWindows() && Debugger.IsAttached)
            {
                return "http://localhost:5120";
            }
            return "https://app.controlr.app";
        }
    }


    [GeneratedRegex("[^A-Za-z0-9_-]")]
    public static partial Regex UsernameValidator();
}
