using ControlR.Agent.Shared.Constants;
using ControlR.Agent.Shared.Options;
using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Shared.Services;

public interface IFileSystemPathProvider
{
  /// <summary>
  /// Returns the path to the agent's appsettings.json file.
  /// </summary>
  string GetAgentAppSettingsPath();
  /// <summary>
  /// Returns the path to the agent executable, which is the install directory combined with the platform-specific agent file name.
  /// </summary>
  string GetAgentExecutablePath();
  /// <summary>
  /// Returns the directory where the agent is installed.
  /// </summary>
  string GetAgentInstallDirectory();
  /// <summary>
  /// Returns the path to the agent's current log file.
  /// </summary>
  string GetAgentLogFilePath();
  /// <summary>
  /// Returns the directory where agent logs are stored. On Windows this is under CommonApplicationData; on Linux/macOS it is under /var/log/controlr for elevated processes or ~/.controlr/logs for user processes.
  /// </summary>
  string GetAgentLogsDirectoryPath();
  /// <summary>
  /// Returns the path where the bundle hash file is stored.
  /// This file contains the SHA-256 hash of the currently installed bundle and is used by the updater
  /// to validate whether the installation is up to date with the server's latest bundle.
  /// </summary>
  string GetBundleHashFilePath();
  /// <summary>
  /// Returns the directory where the agent executable resides, which is also the bundle root after installation.
  /// Currently returns the startup directory; used for centralizing assumptions about where the bundle is installed.
  /// </summary>
  string GetBundleRootDirectory();
  /// <summary>
  /// Returns the directory where macOS bundle state (plist files) is stored.
  /// </summary>
  string GetBundleStateDirectory();
  /// <summary>
  /// Returns the directory where the desktop client is installed. On macOS, this is the app bundle path; on other platforms, it's a "DesktopClient" subdirectory under the agent's startup directory.
  /// </summary>
  string GetDesktopClientDirectory();
  /// <summary>
  /// Returns the path to the desktop client executable. On macOS, this is a specific path inside the app bundle; on other platforms, it's the "DesktopClient" subdirectory under the agent's startup directory combined with the desktop client file name.
  /// </summary>
  /// <returns></returns>
  string GetDesktopExecutablePath();
  /// <summary>
  /// Returns the base directory for .NET single-file bundle extraction, used by DOTNET_BUNDLE_EXTRACT_BASE_DIR.
  /// </summary>
  string GetDotnetExtractDirectory();
  /// <summary>
  /// Returns the effective instance ID, defaulting to "default" if not configured.
  /// </summary>
  string GetEffectiveInstanceId();
  /// <summary>
  /// Returns the log file path for the installer.
  /// </summary>
  string GetInstallerLogFilePath();
  /// <summary>
  /// Returns the directory path for installer logs.
  /// </summary>
  string GetInstallerLogsDirectoryPath();
  /// <summary>
  /// Returns the macOS app bundle path (e.g., /Applications/ControlR.app).
  /// </summary>
  string GetMacAppBundlePath();
  /// <summary>
  /// Returns the service file path for the agent systemd/LaunchDaemon service.
  /// </summary>
  string GetServiceFilePath();
  /// <summary>
  /// Returns the path to the agent executable inside a macOS app bundle (for copying out during install).
  /// </summary>
  string GetSourceAgentPath(string appBundlePath);
  /// <summary>
  /// Returns the Windows registry uninstall key path for ControlR, varied by instance ID.
  /// </summary>
  string GetUninstallKeyPath();
  /// <summary>
  /// Returns the log directory for the desktop client running under the specified user on Linux/macOS (e.g. /home/{username}/.controlr/{instanceId}/logs/ControlR.DesktopClient).
  /// </summary>
  string GetUnixDesktopClientLogsDirectory(string username);
  /// <summary>
  /// Returns the log directory for the desktop client running as root on Linux/macOS (e.g. /var/log/controlr/{instanceId}/ControlR.DesktopClient).
  /// </summary>
  string GetUnixDesktopClientLogsDirectoryForRoot();
  /// <summary>
  /// Returns the log directory for the desktop client on Windows, under CommonApplicationData with debug and instance subdirectories.
  /// </summary>
  string GetWindowsDesktopClientLogsDirectory();
}
public class FileSystemPathProvider(
  ISystemEnvironment systemEnvironment,
  IElevationChecker elevationChecker,
  IFileSystem fileSystem,
  IOptionsMonitor<InstanceOptions> instanceOptions) : IFileSystemPathProvider
{
  private readonly IElevationChecker _elevationChecker = elevationChecker;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IOptionsMonitor<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;

  public string GetAgentAppSettingsPath()
  {
    var dir = GetSettingsDirectory();
    return _fileSystem.JoinPaths(GetPathSeparator(), dir, "appsettings.json");
  }

  public string GetAgentExecutablePath()
  {
    return Path.Combine(GetAgentInstallDirectory(), AppConstants.GetAgentFileName(_systemEnvironment.Platform));
  }

  public string GetAgentInstallDirectory()
  {
    var baseDir = _systemEnvironment.IsDebug
      ? Path.Combine(Path.GetTempPath(), BrandingConstants.WindowsInstallDirectoryName, "Install")
      : _systemEnvironment.Platform switch
      {
        SystemPlatform.Windows => Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "Program Files", BrandingConstants.WindowsInstallDirectoryName),
        SystemPlatform.Linux => $"/usr/local/bin/{BrandingConstants.LinuxInstallDirectoryName}",
        SystemPlatform.MacOs => $"/Library/Application Support/{BrandingConstants.MacInstallDirectoryName}",
        _ => throw new PlatformNotSupportedException()
      };

    var instanceId = GetInstanceId() ?? AppConstants.DefaultInstallDirectoryName;
    return Path.Combine(baseDir, instanceId);
  }

  public string GetAgentLogFilePath()
  {
    return _fileSystem.JoinPaths(GetPathSeparator(), GetAgentLogsDirectoryPath(), "LogFile.log");
  }

  public string GetAgentLogsDirectoryPath()
  {
    if (_systemEnvironment.IsWindows())
    {
      var logsDir = _fileSystem.JoinPaths(GetPathSeparator(),
        _systemEnvironment.GetCommonApplicationDataDirectory(),
        BrandingConstants.WindowsLogDirectoryName);

      logsDir = AppendSubDirectories(logsDir);
      return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Logs", BrandingConstants.AgentBaseName);
    }

    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      var isElevated = _elevationChecker.IsElevated();
      var rootDir = isElevated
        ? $"/var/log/{BrandingConstants.UnixLogDirectoryName}"
        : _fileSystem.JoinPaths(GetPathSeparator(), _systemEnvironment.GetProfileDirectory(), BrandingConstants.UnixHiddenDirectoryName);

      rootDir = AppendSubDirectories(rootDir);
      var logsDir = isElevated ? rootDir : _fileSystem.JoinPaths(GetPathSeparator(), rootDir, "logs");
      return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, BrandingConstants.AgentBaseName);
    }

    throw new PlatformNotSupportedException();
  }

  public string GetBundleHashFilePath()
  {
    var settingsDirectory = GetSettingsDirectory();
    return _fileSystem.JoinPaths(GetPathSeparator(), settingsDirectory, BrandingConstants.BundleHashFileName);
  }

  public string GetBundleRootDirectory()
  {
    if (_systemEnvironment.IsMacOS())
    {
      return GetMacAppBundlePath();
    }

    return GetAgentInstallDirectory();
  }

  public string GetBundleStateDirectory()
  {
    return GetAgentInstallDirectory();
  }

  public string GetDesktopClientDirectory()
  {
    if (_systemEnvironment.IsMacOS())
    {
      return GetMacInstalledAppPath();
    }

    var startupDir = _systemEnvironment.StartupDirectory;
    return Path.Combine(startupDir, BrandingConstants.DesktopClientDirectoryName);
  }

  public string GetDesktopExecutablePath()
  {
    var desktopDir = GetDesktopClientDirectory();

    return _systemEnvironment.Platform switch
    {
      SystemPlatform.MacOs => _fileSystem.JoinPaths('/', desktopDir, PathConstants.MacDesktopExecutableRelativePath),
      _ => Path.Combine(desktopDir, AppConstants.DesktopClientFileName)
    };
  }

  public string GetDotnetExtractDirectory()
  {
    return Path.Combine(GetAgentInstallDirectory(), ".net");
  }

  public string GetEffectiveInstanceId()
  {
    return string.IsNullOrWhiteSpace(_instanceOptions.CurrentValue.InstanceId)
      ? AppConstants.DefaultInstallDirectoryName
      : _instanceOptions.CurrentValue.InstanceId!;
  }

  public string GetInstallerLogFilePath()
  {
    return _fileSystem.JoinPaths(GetPathSeparator(), GetInstallerLogsDirectoryPath(), "LogFile.log");
  }

  public string GetInstallerLogsDirectoryPath()
  {
    if (_systemEnvironment.IsWindows())
    {
      var logsDir = _fileSystem.JoinPaths(GetPathSeparator(),
        _systemEnvironment.GetCommonApplicationDataDirectory(),
        BrandingConstants.WindowsLogDirectoryName);

      logsDir = AppendSubDirectories(logsDir);
      return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Logs", BrandingConstants.InstallerBaseName);
    }

    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      var isElevated = _elevationChecker.IsElevated();
      var rootDir = isElevated
        ? $"/var/log/{BrandingConstants.UnixLogDirectoryName}"
        : _fileSystem.JoinPaths(GetPathSeparator(), _systemEnvironment.GetProfileDirectory(), BrandingConstants.UnixHiddenDirectoryName);

      rootDir = AppendSubDirectories(rootDir);
      var logsDir = isElevated ? rootDir : _fileSystem.JoinPaths(GetPathSeparator(), rootDir, "logs");
      return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, BrandingConstants.InstallerBaseName);
    }

    throw new PlatformNotSupportedException();
  }

  public string GetMacAppBundlePath()
  {
    return _fileSystem.JoinPaths('/', PathConstants.MacApplicationsDirectory, PathConstants.GetMacAppBundleName(GetInstanceId()));
  }

  public string GetServiceFilePath()
  {
    var instanceId = GetInstanceId();
    return _systemEnvironment.Platform switch
    {
      SystemPlatform.Linux => instanceId is null or ""
        ? $"/etc/systemd/system/{BrandingConstants.LinuxAgentServiceName}"
        : $"/etc/systemd/system/{BrandingConstants.LinuxAgentServiceName.Replace(".service", "")}-{instanceId}.service",
      SystemPlatform.MacOs => instanceId is null or ""
        ? $"/Library/LaunchDaemons/{BrandingConstants.MacServicePrefix}.agent.plist"
        : $"/Library/LaunchDaemons/{BrandingConstants.MacServicePrefix}.agent.{instanceId}.plist",
      _ => throw new PlatformNotSupportedException()
    };
  }

  public string GetSourceAgentPath(string appBundlePath)
  {
    return _fileSystem.JoinPaths('/', appBundlePath, "Contents", "Library", "LaunchServices", AppConstants.GetAgentFileName(SystemPlatform.MacOs));
  }

  public string GetUninstallKeyPath()
  {
    var instanceId = GetInstanceId();
    return _systemEnvironment.Platform switch
    {
      SystemPlatform.Windows => instanceId is null or ""
        ? $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{BrandingConstants.WindowsUninstallRegistryKeyName}"
        : $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{BrandingConstants.WindowsUninstallRegistryKeyName} ({instanceId})",
      _ => throw new PlatformNotSupportedException()
    };
  }

  public string GetUnixDesktopClientLogsDirectory(string username)
  {
    if (!_systemEnvironment.IsLinux() && !_systemEnvironment.IsMacOS())
    {
      throw new PlatformNotSupportedException();
    }

    if (string.IsNullOrWhiteSpace(username))
    {
      throw new ArgumentException("Username must be provided for non-root log directory.", nameof(username));
    }

    var instanceId = GetEffectiveInstanceId();
    var homeRoot = _systemEnvironment.IsMacOS() ? "/Users" : "/home";

    return _fileSystem.JoinPaths(GetPathSeparator(), homeRoot, username, BrandingConstants.UnixHiddenDirectoryName, instanceId, "logs", BrandingConstants.DesktopClientBaseName);
  }

  public string GetUnixDesktopClientLogsDirectoryForRoot()
  {
    if (!_systemEnvironment.IsLinux() && !_systemEnvironment.IsMacOS())
    {
      throw new PlatformNotSupportedException();
    }

    var instanceId = GetEffectiveInstanceId();
    var logsDir = $"/var/log/{BrandingConstants.UnixLogDirectoryName}";

    logsDir = _fileSystem.JoinPaths(GetPathSeparator(), logsDir, instanceId);

    return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, BrandingConstants.DesktopClientBaseName);
  }

  public string GetWindowsDesktopClientLogsDirectory()
  {
    if (!_systemEnvironment.IsWindows())
    {
      throw new PlatformNotSupportedException();
    }

    var isDebug = _systemEnvironment.IsDebug;

    var logsDir = _fileSystem.JoinPaths(GetPathSeparator(),
         _systemEnvironment.GetCommonApplicationDataDirectory(),
         BrandingConstants.WindowsLogDirectoryName);

    if (isDebug)
    {
      logsDir = _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Debug");
    }

    logsDir = _fileSystem.JoinPaths(GetPathSeparator(), logsDir, GetEffectiveInstanceId());

    return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Logs", BrandingConstants.DesktopClientBaseName);

  }

  private string AppendSubDirectories(string rootDir)
  {
    var instanceId = GetEffectiveInstanceId();

    if (_systemEnvironment.IsWindows())
    {
      if (_systemEnvironment.IsDebug)
      {
        rootDir = _fileSystem.JoinPaths(GetPathSeparator(), rootDir, "Debug");
      }

      rootDir = _fileSystem.JoinPaths(GetPathSeparator(), rootDir, instanceId);

      _ = _fileSystem.CreateDirectory(rootDir).FullName;
      return rootDir;
    }

    // ReSharper disable once InvertIf
    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      rootDir = _fileSystem.JoinPaths(GetPathSeparator(), rootDir, instanceId);

      _ = _fileSystem.CreateDirectory(rootDir).FullName;
      return rootDir;
    }

    throw new PlatformNotSupportedException();
  }

  private string? GetInstanceId()
  {
    return string.IsNullOrWhiteSpace(_instanceOptions.CurrentValue.InstanceId)
      ? null
      : _instanceOptions.CurrentValue.InstanceId;
  }

  private string GetMacInstalledAppPath()
  {
    return PathConstants.GetMacInstalledAppPath(_instanceOptions.CurrentValue.InstanceId);
  }

  private char GetPathSeparator()
  {
    return _systemEnvironment.IsWindows() ? '\\' : '/';
  }

  private string GetSettingsDirectory()
  {
    if (_systemEnvironment.IsWindows())
    {
      var rootDir = _fileSystem.JoinPaths(
        GetPathSeparator(),
        _systemEnvironment.GetCommonApplicationDataDirectory(),
        BrandingConstants.WindowsInstallDirectoryName);

      return AppendSubDirectories(rootDir);
    }

    // ReSharper disable once InvertIf
    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      var rootDir = _elevationChecker.IsElevated()
        ? $"/etc/{BrandingConstants.UnixConfigDirectoryName}"
        : _fileSystem.JoinPaths(GetPathSeparator(), _systemEnvironment.GetProfileDirectory(), BrandingConstants.UnixHiddenDirectoryName);

      return AppendSubDirectories(rootDir);
    }

    throw new PlatformNotSupportedException();
  }

}
