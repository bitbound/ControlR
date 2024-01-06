using ControlR.Shared.Enums;
using ControlR.Shared.Services;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ControlR.Shared;

public static partial class AppConstants
{
    public const string AgentCertificateThumbprint = "4b6235f1c44ab3a5f29bf40ad85b442269f6ee52";
    private const string DevServerUri = "http://localhost:5120";
    private const string ProdServerUri = "https://app.controlr.app";

    public static string AgentFileName
    {
        get
        {
            return EnvironmentHelper.Instance.Platform switch
            {
                SystemPlatform.Windows => "ControlR.Agent.exe",
                SystemPlatform.Linux => "ControlR.Agent",
                SystemPlatform.MacOS => "ControlR.Agent",
                _ => throw new PlatformNotSupportedException(),
            };
        }
    }

    public static string ExternalDownloadsUri => "https://controlr.app";

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
                return DevServerUri;
            }
            return ProdServerUri;
        }
    }

    public static string TightVncZipName { get; } = "tvnserver.zip";

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

    [GeneratedRegex("[^A-Za-z0-9_-]")]
    public static partial Regex UsernameValidator();
}