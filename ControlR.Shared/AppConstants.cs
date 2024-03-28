﻿using ControlR.Shared.Enums;
using ControlR.Shared.Services;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ControlR.Shared;

public static partial class AppConstants
{
    private const string DevServerUri = "http://localhost:5120";
    private const string ProdServerUri = "https://app.controlr.app";

    public static string AgentFileName
    {
        get
        {
            return EnvironmentHelper.Instance.Platform switch
            {
                SystemPlatform.Windows => "ControlR.Agent.exe",
                SystemPlatform.Android => "ControlR.Agent.exe",
                SystemPlatform.Linux => "ControlR.Agent",
                SystemPlatform.MacOS => "ControlR.Agent",
                _ => throw new PlatformNotSupportedException(),
            };
        }
    }

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
                SystemPlatform.Android => "controlr-streamer-win.zip",
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

    [GeneratedRegex("[^A-Za-z0-9_-]")]
    public static partial Regex UsernameValidator();
}