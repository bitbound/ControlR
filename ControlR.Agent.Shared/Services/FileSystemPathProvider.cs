using ControlR.Agent.Shared.Constants;
using ControlR.Agent.Shared.Options;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Shared.Services;

public interface IFileSystemPathProvider
{
  string GetAgentAppSettingsPath();
  string GetAgentExecutablePath();
  /// <summary>
  /// Returns the directory where the agent is installed.
  /// </summary>
  string GetAgentInstallDirectory();
  string GetAgentLogFilePath();
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
  string GetDesktopClientDirectory();
  string GetDesktopExecutablePath();
  /// <summary>
  /// Returns the base directory for .NET single-file bundle extraction, used by DOTNET_BUNDLE_EXTRACT_BASE_DIR.
  /// </summary>
  string GetDotnetExtractDirectory();
  string GetInstallerLogFilePath();
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
  string GetUninstallKeyPath();
  string GetUnixDesktopClientLogsDirectory(string username);
  string GetUnixDesktopClientLogsDirectoryForRoot();
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
      ? Path.Combine(Path.GetTempPath(), "ControlR", "Install")
      : _systemEnvironment.Platform switch
      {
        SystemPlatform.Windows => Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "Program Files", "ControlR"),
        SystemPlatform.Linux => "/usr/local/bin/ControlR",
        SystemPlatform.MacOs => "/Library/Application Support/ControlR",
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
        "ControlR");

      logsDir = AppendSubDirectories(logsDir);
      return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Logs", "ControlR.Agent");
    }

    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      var isElevated = _elevationChecker.IsElevated();
      var rootDir = isElevated
        ? "/var/log/controlr"
        : _fileSystem.JoinPaths(GetPathSeparator(), _systemEnvironment.GetProfileDirectory(), ".controlr");

      rootDir = AppendSubDirectories(rootDir);
      var logsDir = isElevated ? rootDir : _fileSystem.JoinPaths(GetPathSeparator(), rootDir, "logs");
      return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "ControlR.Agent");
    }

    throw new PlatformNotSupportedException();
  }

  public string GetBundleHashFilePath()
  {
    var settingsDirectory = GetSettingsDirectory();
    return _fileSystem.JoinPaths(GetPathSeparator(), settingsDirectory, ".controlr-bundle.sha256");
  }

  public string GetBundleRootDirectory()
  {
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
    return Path.Combine(startupDir, "DesktopClient");
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
        "ControlR");

      logsDir = AppendSubDirectories(logsDir);
      return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Logs", "ControlR.Agent.Installer");
    }

    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      var isElevated = _elevationChecker.IsElevated();
      var rootDir = isElevated
        ? "/var/log/controlr"
        : _fileSystem.JoinPaths(GetPathSeparator(), _systemEnvironment.GetProfileDirectory(), ".controlr");

      rootDir = AppendSubDirectories(rootDir);
      var logsDir = isElevated ? rootDir : _fileSystem.JoinPaths(GetPathSeparator(), rootDir, "logs");
      return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "ControlR.Agent.Installer");
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
        ? "/etc/systemd/system/controlr.agent.service"
        : $"/etc/systemd/system/controlr.agent-{instanceId}.service",
      SystemPlatform.MacOs => instanceId is null or ""
        ? "/Library/LaunchDaemons/app.controlr.agent.plist"
        : $"/Library/LaunchDaemons/app.controlr.agent.{instanceId}.plist",
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
        ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ControlR"
        : $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ControlR ({instanceId})",
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

    return _fileSystem.JoinPaths(GetPathSeparator(), homeRoot, username, ".controlr", instanceId, "logs", "ControlR.DesktopClient");
  }

  public string GetUnixDesktopClientLogsDirectoryForRoot()
  {
    if (!_systemEnvironment.IsLinux() && !_systemEnvironment.IsMacOS())
    {
      throw new PlatformNotSupportedException();
    }

    var instanceId = GetEffectiveInstanceId();
    var logsDir = "/var/log/controlr";

    logsDir = _fileSystem.JoinPaths(GetPathSeparator(), logsDir, instanceId);

    return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "ControlR.DesktopClient");
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
         "ControlR");

    if (isDebug)
    {
      logsDir = _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Debug");
    }

    logsDir = _fileSystem.JoinPaths(GetPathSeparator(), logsDir, GetEffectiveInstanceId());

    return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Logs", "ControlR.DesktopClient");

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

  private string GetEffectiveInstanceId()
  {
    return string.IsNullOrWhiteSpace(_instanceOptions.CurrentValue.InstanceId)
      ? AppConstants.DefaultInstallDirectoryName
      : _instanceOptions.CurrentValue.InstanceId!;
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
        "ControlR");

      return AppendSubDirectories(rootDir);
    }

    // ReSharper disable once InvertIf
    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      var rootDir = _elevationChecker.IsElevated()
        ? "/etc/controlr"
        : _fileSystem.JoinPaths(GetPathSeparator(), _systemEnvironment.GetProfileDirectory(), ".controlr");

      return AppendSubDirectories(rootDir);
    }

    throw new PlatformNotSupportedException();
  }

}
