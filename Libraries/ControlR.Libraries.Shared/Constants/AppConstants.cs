using System.Diagnostics;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Services;

namespace ControlR.Libraries.Shared.Constants;

public static class AppConstants
{
  public const string AgentHubPath = "/hubs/agent";
  public const int DefaultHubDtoChunkSize = 100;
  public const string DefaultInstanceId = "default";
  public const double DefaultRemoteControlAutoQualityLowerThresholdMbps = 5d;
  public const int DefaultRemoteControlAutoQualityMaximum = 80;
  public const int DefaultRemoteControlAutoQualityMinimum = 20;
  public const double DefaultRemoteControlAutoQualityUpperThresholdMbps = 15d;
  public const bool DefaultRemoteControlCaptureCursor = false;
  public const bool DefaultRemoteControlIsAutoQualityEnabled = false;
  public const bool DefaultRemoteControlIsMaxBandwidthEnabled = false;
  public const int DefaultRemoteControlManualQuality = 75;
  public const double DefaultRemoteControlMaxBandwidthMbps = 15d;
  public const int DefaultVncPort = 5900;
  public const string ViewerHubPath = "/hubs/viewer";
  public const string WebSocketRelayPath = "/relay";

  public static string DesktopClientFileName =>
    SystemEnvironment.Instance.Platform switch
    {
      SystemPlatform.Windows => $"{BrandingConstants.DesktopClientBaseName}.exe",
      SystemPlatform.Linux or SystemPlatform.MacOs => BrandingConstants.DesktopClientBaseName,
      _ => throw new PlatformNotSupportedException()
    };
  public static string FfmpegFileName =>
    SystemEnvironment.Instance.Platform switch
    {
      SystemPlatform.Windows => "ffmpeg.exe",
      SystemPlatform.Linux or SystemPlatform.MacOs => "ffmpeg",
      _ => throw new PlatformNotSupportedException()
    };
  public static Uri? ServerUri
  {
    get
    {
      if (OperatingSystem.IsWindows() && Debugger.IsAttached)
      {
        return DevServerUri;
      }

      return null;
    }
  }
  public static int SignalrMaxMessageSize => 30 * 1024; // 30KB.

  private static Uri DevServerUri { get; } = new("http://localhost:5120");

  public static string GetAgentFileName(SystemPlatform platform)
  {
    return platform switch
    {
      SystemPlatform.Windows => $"{BrandingConstants.AgentBaseName}.exe",
      SystemPlatform.Android => $"{BrandingConstants.AgentBaseName}.exe",
      SystemPlatform.Linux => BrandingConstants.AgentBaseName,
      SystemPlatform.MacOs => BrandingConstants.AgentBaseName,
      _ => throw new PlatformNotSupportedException()
    };
  }

  /// <summary>
  /// Gets the download path for the bundle ZIP containing both Agent and DesktopClient for a specific runtime.
  /// </summary>
  public static string GetBundleZipDownloadPath(RuntimeId runtime)
  {
    return runtime switch
    {
      RuntimeId.WinX64 => $"/downloads/win-x64/{BrandingConstants.BundleZipBaseName}.zip",
      RuntimeId.WinX86 => $"/downloads/win-x86/{BrandingConstants.BundleZipBaseName}.zip",
      RuntimeId.LinuxX64 => $"/downloads/linux-x64/{BrandingConstants.BundleZipBaseName}.zip",
      RuntimeId.MacOsX64 => $"/downloads/osx-x64/{BrandingConstants.BundleZipBaseName}.zip",
      RuntimeId.MacOsArm64 => $"/downloads/osx-arm64/{BrandingConstants.BundleZipBaseName}.zip",
      _ => throw new PlatformNotSupportedException()
    };
  }

  /// <summary>
  /// Gets the download path for the bootstrap installer for a specific runtime.
  /// </summary>
  public static string GetInstallerDownloadPath(RuntimeId runtime)
  {
    var installerBase = BrandingConstants.InstallerBaseName;
    return runtime switch
    {
      RuntimeId.WinX64 => $"/downloads/win-x64/{installerBase}.exe",
      RuntimeId.WinX86 => $"/downloads/win-x86/{installerBase}.exe",
      RuntimeId.LinuxX64 => $"/downloads/linux-x64/{installerBase}",
      RuntimeId.MacOsX64 => $"/downloads/osx-x64/{installerBase}",
      RuntimeId.MacOsArm64 => $"/downloads/osx-arm64/{installerBase}",
      _ => throw new PlatformNotSupportedException()
    };
  }

  public static string GetInstallerFileName(SystemPlatform platform)
  {
    var installerBase = BrandingConstants.InstallerBaseName;
    return platform switch
    {
      SystemPlatform.Windows => $"{installerBase}.exe",
      SystemPlatform.Android => $"{installerBase}.exe",
      SystemPlatform.Linux => installerBase,
      SystemPlatform.MacOs => installerBase,
      _ => throw new PlatformNotSupportedException()
    };
  }
}
