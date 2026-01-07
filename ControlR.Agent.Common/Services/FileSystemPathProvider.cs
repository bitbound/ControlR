using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Startup;
using ControlR.Libraries.DevicesCommon.Services;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services;

public interface IFileSystemPathProvider
{
  string GetAgentAppSettingsPath();

  string GetAgentLogFilePath();

  string GetAgentLogsDirectoryPath();

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

    var instanceId = _instanceOptions.CurrentValue.InstanceId;
    var homeRoot = _systemEnvironment.IsMacOS() ? "/Users" : "/home";

    return _fileSystem.JoinPaths(GetPathSeparator(), homeRoot, username, ".controlr", instanceId ?? string.Empty, "logs", "ControlR.DesktopClient");
  }

  public string GetUnixDesktopClientLogsDirectoryForRoot()
  {
    if (!_systemEnvironment.IsLinux() && !_systemEnvironment.IsMacOS())
    {
      throw new PlatformNotSupportedException();
    }

    var instanceId = _instanceOptions.CurrentValue.InstanceId;
    var logsDir = "/var/log/controlr";
    if (!string.IsNullOrWhiteSpace(instanceId))
    {
      logsDir = _fileSystem.JoinPaths(GetPathSeparator(), logsDir, instanceId);
    }
    return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "ControlR.DesktopClient");
  }

  public string GetWindowsDesktopClientLogsDirectory()
  {
    if (!_systemEnvironment.IsWindows())
    {
      throw new PlatformNotSupportedException();
    }

    var instanceId = _instanceOptions.CurrentValue.InstanceId;
    var isDebug = _systemEnvironment.IsDebug;

    var logsDir = _fileSystem.JoinPaths(GetPathSeparator(),
         _systemEnvironment.GetCommonApplicationDataDirectory(),
         "ControlR");

    if (isDebug)
    {
      logsDir = _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Debug");
    }

    if (!string.IsNullOrWhiteSpace(instanceId))
    {
      logsDir = _fileSystem.JoinPaths(GetPathSeparator(), logsDir, instanceId);
    }

    return _fileSystem.JoinPaths(GetPathSeparator(), logsDir, "Logs", "ControlR.DesktopClient");

  }

  private string AppendSubDirectories(string rootDir)
  {
    var instanceId = _instanceOptions.CurrentValue.InstanceId;

    if (_systemEnvironment.IsWindows())
    {
      if (_systemEnvironment.IsDebug)
      {
        rootDir = _fileSystem.JoinPaths(GetPathSeparator(), rootDir, "Debug");
      }

      if (!string.IsNullOrWhiteSpace(instanceId))
      {
        rootDir = _fileSystem.JoinPaths(GetPathSeparator(), rootDir, instanceId);
      }

      _ = _fileSystem.CreateDirectory(rootDir).FullName;
      return rootDir;
    }

    // ReSharper disable once InvertIf
    if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
    {
      if (!string.IsNullOrWhiteSpace(instanceId))
      {
        rootDir = _fileSystem.JoinPaths(GetPathSeparator(), rootDir, instanceId);
      }

      _ = _fileSystem.CreateDirectory(rootDir).FullName;
      return rootDir;
    }

    throw new PlatformNotSupportedException();
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
