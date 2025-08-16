using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.NativeInterop.Windows;
using Microsoft.Extensions.Hosting;
using ControlR.Libraries.DevicesCommon.Services.Processes;

namespace ControlR.Agent.Common.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class DesktopClientWatcherWin(
  TimeProvider timeProvider,
  IWin32Interop win32Interop,
  IProcessManager processManager,
  ISystemEnvironment environment,
  IFileSystem fileSystem,
  ISettingsProvider settingsProvider,
  IDesktopClientUpdater desktopClientUpdater,
  ILogger<DesktopClientWatcherWin> logger) : BackgroundService
{
  private readonly IDesktopClientUpdater _desktopClientUpdater = desktopClientUpdater;
  private readonly ISystemEnvironment _environment = environment;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<DesktopClientWatcherWin> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  private readonly ISettingsProvider _settingsProvider = settingsProvider;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IWin32Interop _win32Interop = win32Interop;
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
      try
      {
        var activeSessions = _win32Interop.GetActiveSessions();
        var launchTasks = new List<Task<bool>>();

        foreach (var session in activeSessions)
        {
          var desktopProcessRunning = _processManager
              .GetProcessesByName(Path.GetFileNameWithoutExtension(AppConstants.DesktopClientFileName))
              .Any(x => x.SessionId == session.SystemSessionId);

          if (!desktopProcessRunning)
          {
            _logger.LogInformation("No desktop client found in session {SessionId}. Launching a new one.", session.SystemSessionId);
            var launchTask = LaunchDesktopClient(session.SystemSessionId, stoppingToken);
            launchTasks.Add(launchTask);
          }
        }
        await Task.WhenAll(launchTasks);
        if (launchTasks.Any(x => !x.Result))
        {
          await DeleteDesktopClient();
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while checking for desktop client processes.");
      }
    }
  }

  // For debugging.
  private static Result<string> GetSolutionDir(string currentDir)
  {
    var dirInfo = new DirectoryInfo(currentDir);
    if (!dirInfo.Exists)
    {
      return Result.Fail<string>("Not found.");
    }

    if (dirInfo.GetFiles().Any(x => x.Name == "ControlR.sln"))
    {
      return Result.Ok(currentDir);
    }

    if (dirInfo.Parent is not null)
    {
      return GetSolutionDir(dirInfo.Parent.FullName);
    }

    return Result.Fail<string>("Not found.");
  }

  // Kills all desktop client processes, deletes the archive, and deletes the folder.
  private async Task DeleteDesktopClient()
  {
    try
    {
      var processName = Path.GetFileNameWithoutExtension(AppConstants.DesktopClientFileName);
      var processes = _processManager.GetProcessesByName(processName);
      foreach (var process in processes)
      {
        try
        {
          process.KillAndDispose();
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Failed to kill desktop client process {ProcessId}.", process.Id);
        }
      }

      var startupDir = _environment.StartupDirectory;
      var desktopDir = Path.Combine(startupDir, "DesktopClient");
      var zipPath = Path.Combine(startupDir, AppConstants.DesktopClientZipFileName);

      if (_fileSystem.FileExists(zipPath))
      {
        try
        {
          _fileSystem.DeleteFile(zipPath);
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Failed to delete desktop client archive: {ZipPath}", zipPath);
        }
      }

      if (_fileSystem.DirectoryExists(desktopDir))
      {
        try
        {
          _fileSystem.DeleteDirectory(desktopDir, true);
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Failed to delete desktop client directory: {DesktopDir}", desktopDir);
        }
      }

      _logger.LogInformation("Desktop client removed and will be reinstalled.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while deleting desktop client files.");
    }
    await Task.CompletedTask;
  }

  private async Task<bool> LaunchDesktopClient(int sessionId, CancellationToken cancellationToken)
  {
    try
    {
      if (_environment.IsDebug)
      {
        StartDebugSession();
        return true;
      }

      await _desktopClientUpdater.EnsureLatestVersion(cancellationToken);
      var startupDir = _environment.StartupDirectory;
      var desktopDir = Path.Combine(startupDir, "DesktopClient");
      var binaryPath = Path.Combine(desktopDir, AppConstants.DesktopClientFileName);

      var result = _win32Interop.CreateInteractiveSystemProcess(
        commandLine: $"\"{binaryPath}\" --instance-id {_settingsProvider.InstanceId}",
        targetSessionId: sessionId,
        hiddenWindow: true,
        startedProcess: out var process);

      if (!result || process is null || process.Id == -1)
      {
        _logger.LogError("Failed to start desktop client process in session {SessionId}.", sessionId);
        return false;
      }

      // Wait to make sure the process stays running.
      await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, cancellationToken);
      if (process.HasExited)
      {
        _logger.LogError("Desktop client process in session {SessionId} has exited immediately after launch.", sessionId);
        return false;
      }
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while launching desktop client in session {SessionId}.", sessionId);
      return false;
    }
  }

  private void StartDebugSession()
  {
    var solutionDirResult = GetSolutionDir(Environment.CurrentDirectory);

    if (!solutionDirResult.IsSuccess)
    {
      return;
    }

    var desktopClientBin = Path.Combine(
          solutionDirResult.Value,
          "ControlR.DesktopClient",
          "bin",
          "Debug");

    var desktopClientPath = _fileSystem
      .GetFiles(desktopClientBin, AppConstants.DesktopClientFileName, SearchOption.AllDirectories)
      .OrderByDescending(x => new FileInfo(x).CreationTime)
      .FirstOrDefault();

    if (string.IsNullOrWhiteSpace(desktopClientPath))
    {
      throw new FileNotFoundException("DesktopClient binary not found.", desktopClientPath);
    }

    var psi = new ProcessStartInfo()
    {
      WorkingDirectory = Path.GetDirectoryName(desktopClientPath),
      UseShellExecute = true,
      FileName = desktopClientPath,
      Arguments = "--startup-mode Child"
    };

    _processManager.Start(psi);
  }
}