using ControlR.Agent.Shared.Constants;
using ControlR.Agent.Shared.Options;
using ControlR.Libraries.Branding;
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
  /// Returns the install directory that <paramref name="publisherName"/> and <paramref name="brandName"/>
  /// would use on this machine. Like <see cref="GetSettingsDirectoryFor"/>, this describes a brand other
  /// than the one compiled into this build, and a vendor directory is part of that brand's path, so both
  /// names are needed to address an install that is not this one.
  /// </summary>
  string GetAgentInstallDirectoryFor(string? publisherName, string brandName, string? instanceId);
  /// <summary>
  /// Returns the path to the agent's current log file.
  /// </summary>
  string GetAgentLogFilePath();
  /// <summary>
  /// Returns the directory where agent logs are stored. Every root here sits under a publisher directory:
  /// on Windows it is under CommonApplicationData, on Linux/macOS under /var/log for elevated processes or
  /// the user's hidden home directory for user processes.
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
  /// Returns the settings directory that <paramref name="publisherName"/> and <paramref name="brandName"/>
  /// would use on this machine, which is where a rebrand migration reads appsettings.json from.
  /// Unlike the other members, this describes a brand other than the one compiled into this build.
  /// </summary>
  string GetSettingsDirectoryFor(string? publisherName, string brandName, string? instanceId);
  /// <summary>
  /// Returns the path to the agent executable inside a macOS app bundle (for copying out during install).
  /// </summary>
  string GetSourceAgentPath(string appBundlePath);
  /// <summary>
  /// Returns the Windows registry uninstall key path for ControlR, varied by instance ID.
  /// </summary>
  string GetUninstallKeyPath();
  /// <summary>
  /// Returns the log directory for the desktop client running under the specified user on Linux/macOS (e.g. /home/{username}/.bitbound/controlr/{instanceId}/logs/ControlR.DesktopClient).
  /// </summary>
  string GetUnixDesktopClientLogsDirectory(string username);
  /// <summary>
  /// Returns the log directory for the desktop client running as root on Linux/macOS (e.g. /var/log/bitbound/controlr/{instanceId}/ControlR.DesktopClient).
  /// </summary>
  string GetUnixDesktopClientLogsDirectoryForRoot();
  /// <summary>
  /// Returns the log directory for the desktop client running as the specified user on Windows,
  /// under CommonApplicationData with debug and instance subdirectories.
  /// </summary>
  string GetWindowsDesktopClientLogsDirectory(string username);
  /// <summary>
  /// Returns the directory whose subdirectories are the Windows users that have run the desktop client.
  /// </summary>
  string GetWindowsDesktopClientUsersDirectory();
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
    return _fileSystem.JoinPaths(GetPathSeparator(), GetAgentInstallDirectory(), AppConstants.GetAgentFileName(_systemEnvironment.Platform));
  }

  public string GetAgentInstallDirectory()
  {
    return GetAgentInstallDirectoryFor(BrandingConstants.Publisher, BrandingConstants.BrandName, GetInstanceId());
  }

  public string GetAgentInstallDirectoryFor(string? publisherName, string brandName, string? instanceId)
  {
    var brandDirectories = InstallPathNames.Directories(publisherName, brandName);

    var baseDir = _systemEnvironment.IsDebug
      ? _fileSystem.JoinPaths(GetPathSeparator(), [Path.GetTempPath().TrimEnd(GetPathSeparator()), .. brandDirectories, "Install"])
      : _systemEnvironment.Platform switch
      {
        SystemPlatform.Windows => _fileSystem.JoinPaths(GetPathSeparator(), [_systemEnvironment.GetProgramFilesDirectory(), .. brandDirectories]),
        SystemPlatform.Linux => _fileSystem.JoinPaths(GetPathSeparator(), ["/usr/local/bin", .. brandDirectories]),
        SystemPlatform.MacOs => _fileSystem.JoinPaths(GetPathSeparator(), ["/Library/Application Support", .. brandDirectories]),
        _ => throw new PlatformNotSupportedException()
      };

    var resolvedInstanceId = string.IsNullOrWhiteSpace(instanceId)
      ? AppConstants.DefaultInstanceId
      : instanceId.SanitizeForFileSystem();

    return _fileSystem.JoinPaths(GetPathSeparator(), baseDir, resolvedInstanceId);
  }

  public string GetAgentLogFilePath()
  {
    return _fileSystem.JoinPaths(GetPathSeparator(), GetAgentLogsDirectoryPath(), "LogFile.log");
  }

  public string GetAgentLogsDirectoryPath()
  {
    if (_systemEnvironment.IsWindows())
    {
      var logsDir = AppendSubDirectories(GetWindowsDataRoot());
      return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Logs", BrandingConstants.AgentBaseName);
    }

    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      var isElevated = _elevationChecker.IsElevated();
      var rootDir = isElevated
        ? GetUnixLogDirectoryRoot()
        : GetUnixUserDataRoot();

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
    return _fileSystem.JoinPaths(GetPathSeparator(), startupDir, BrandingConstants.DesktopClientDirectoryName);
  }

  public string GetDesktopExecutablePath()
  {
    var desktopDir = GetDesktopClientDirectory();

    return _systemEnvironment.Platform switch
    {
      SystemPlatform.MacOs => _fileSystem.JoinPaths('/', desktopDir, PathConstants.MacDesktopExecutableRelativePath),
      _ => _fileSystem.JoinPaths(GetPathSeparator(), desktopDir, AppConstants.DesktopClientFileName)
    };
  }

  public string GetDotnetExtractDirectory()
  {
    return _fileSystem.JoinPaths(GetPathSeparator(), GetAgentInstallDirectory(), ".net");
  }

  public string GetEffectiveInstanceId()
  {
    return string.IsNullOrWhiteSpace(_instanceOptions.CurrentValue.InstanceId)
      ? AppConstants.DefaultInstanceId
      : _instanceOptions.CurrentValue.InstanceId;
  }

  public string GetInstallerLogFilePath()
  {
    return _fileSystem.JoinPaths(GetPathSeparator(), GetInstallerLogsDirectoryPath(), "LogFile.log");
  }

  public string GetInstallerLogsDirectoryPath()
  {
    if (_systemEnvironment.IsWindows())
    {
      var logsDir = AppendSubDirectories(GetWindowsDataRoot());
      return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Logs", BrandingConstants.InstallerBaseName);
    }

    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      var isElevated = _elevationChecker.IsElevated();
      var rootDir = isElevated
        ? GetUnixLogDirectoryRoot()
        : GetUnixUserDataRoot();

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

  public string GetSettingsDirectoryFor(string? publisherName, string brandName, string? instanceId)
  {
    var resolvedInstanceId = string.IsNullOrWhiteSpace(instanceId)
      ? AppConstants.DefaultInstanceId
      : instanceId.SanitizeForFileSystem();

    if (_systemEnvironment.IsWindows())
    {
      return AppendSubDirectories(GetWindowsDataRoot(publisherName, brandName), resolvedInstanceId);
    }

    // ReSharper disable once InvertIf
    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      var rootDir = _elevationChecker.IsElevated()
        ? _fileSystem.JoinPaths(GetPathSeparator(), ["/etc", .. InstallPathNames.LowercaseDirectories(publisherName, brandName)])
        : GetUnixUserDataRoot(publisherName, brandName);

      return AppendSubDirectories(rootDir, resolvedInstanceId);
    }

    throw new PlatformNotSupportedException();
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

    return _fileSystem.JoinPaths(
      GetPathSeparator(),
      [
        homeRoot,
        username,
        .. InstallPathNames.HiddenHomeDirectories(BrandingConstants.Publisher, BrandingConstants.BrandName),
        instanceId,
        "logs",
        BrandingConstants.DesktopClientBaseName
      ]);
  }

  public string GetUnixDesktopClientLogsDirectoryForRoot()
  {
    if (!_systemEnvironment.IsLinux() && !_systemEnvironment.IsMacOS())
    {
      throw new PlatformNotSupportedException();
    }

    var instanceId = GetEffectiveInstanceId();
    var logsDir = GetUnixLogDirectoryRoot();

    logsDir = _fileSystem.JoinPaths(GetPathSeparator(), logsDir, instanceId);

    return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, BrandingConstants.DesktopClientBaseName);
  }

  public string GetWindowsDesktopClientLogsDirectory(string username)
  {
    if (!_systemEnvironment.IsWindows())
    {
      throw new PlatformNotSupportedException();
    }

    if (string.IsNullOrWhiteSpace(username))
    {
      throw new ArgumentException("Username must be provided for the desktop client log directory.", nameof(username));
    }

    var usersDir = GetWindowsDesktopClientUsersDirectory();

    return _fileSystem.JoinPaths(
      GetPathSeparator(),
      usersDir,
      username,
      BrandingConstants.DesktopClientDirectoryName,
      "Logs");
  }

  public string GetWindowsDesktopClientUsersDirectory()
  {
    if (!_systemEnvironment.IsWindows())
    {
      throw new PlatformNotSupportedException();
    }

    return AppendSubDirectories(GetWindowsDataRoot());
  }

  private string AppendSubDirectories(string rootDir)
  {
    return AppendSubDirectories(rootDir, GetEffectiveInstanceId());
  }

  private string AppendSubDirectories(string rootDir, string instanceId)
  {
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
    return GetSettingsDirectoryFor(BrandingConstants.Publisher, BrandingConstants.BrandName, GetEffectiveInstanceId());
  }

  // macOS exposes /var as a link to /private/var. The log-contents guard refuses
  // a link anywhere along a log path, so the elevated roots name the real path.
  private string GetUnixLogDirectoryRoot()
  {
    var varLogDirectory = _systemEnvironment.IsMacOS() ? "/private/var/log" : "/var/log";

    return _fileSystem.JoinPaths(
      GetPathSeparator(),
      [varLogDirectory, .. InstallPathNames.LowercaseDirectories(BrandingConstants.Publisher, BrandingConstants.BrandName)]);
  }

  /// <summary>
  /// The brand-owned directory in the current user's profile on Linux and macOS, used for settings and
  /// logs when the process is not elevated.
  /// </summary>
  private string GetUnixUserDataRoot()
  {
    return GetUnixUserDataRoot(BrandingConstants.Publisher, BrandingConstants.BrandName);
  }

  private string GetUnixUserDataRoot(string? publisherName, string brandName)
  {
    return _fileSystem.JoinPaths(
      GetPathSeparator(),
      [_systemEnvironment.GetProfileDirectory(), .. InstallPathNames.HiddenHomeDirectories(publisherName, brandName)]);
  }

  /// <summary>
  /// The CommonApplicationData directory this install's brand owns, which holds both its settings and
  /// its logs. The desktop client's per-user directories live under here too.
  /// </summary>
  private string GetWindowsDataRoot()
  {
    return GetWindowsDataRoot(BrandingConstants.Publisher, BrandingConstants.BrandName);
  }

  private string GetWindowsDataRoot(string? publisherName, string brandName)
  {
    return _fileSystem.JoinPaths(
      GetPathSeparator(),
      [_systemEnvironment.GetCommonApplicationDataDirectory(), .. InstallPathNames.Directories(publisherName, brandName)]);
  }
}
