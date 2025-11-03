using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class DesktopClientWatcherWin(
  TimeProvider timeProvider,
  IWin32Interop win32Interop,
  IProcessManager processManager,
  IIpcServerStore ipcServerStore,
  ISystemEnvironment environment,
  IFileSystem fileSystem,
  ISettingsProvider settingsProvider,
  IControlrMutationLock mutationLock,
  IDesktopSessionProvider desktopSessionProvider,
  IDesktopClientUpdater desktopClientUpdater,
  IWaiter waiter,
  ILogger<DesktopClientWatcherWin> logger) : BackgroundService
{
  private readonly IDesktopClientUpdater _desktopClientUpdater = desktopClientUpdater;
  private readonly IDesktopSessionProvider _desktopSessionProvider = desktopSessionProvider;
  private readonly ISystemEnvironment _environment = environment;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IIpcServerStore _ipcServerStore = ipcServerStore;
  private readonly ILogger<DesktopClientWatcherWin> _logger = logger;
  private readonly IControlrMutationLock _mutationLock = mutationLock;
  private readonly IProcessManager _processManager = processManager;
  private readonly ISettingsProvider _settingsProvider = settingsProvider;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IWaiter _waiter = waiter;
  private readonly IWin32Interop _win32Interop = win32Interop;
  private int _launchFailCount;


  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), _timeProvider);

    while (await timer.WaitForNextTick(false, stoppingToken))
    {
      try
      {
        using var mutationLock = await _mutationLock.AcquireAsync(stoppingToken);
        var activeSessions = _win32Interop.GetActiveSessions();
        var desktopClients = await _desktopSessionProvider.GetActiveDesktopClients();

        foreach (var session in activeSessions)
        {
          if (desktopClients.Any(x => x.SystemSessionId == session.SystemSessionId))
          {
            continue;
          }

          _logger.LogInformation(
            "No desktop client found in session {SessionId}. Launching a new one.",
            session.SystemSessionId);

          if (!await _desktopClientUpdater.EnsureLatestVersion(acquireGlobalLock: false, stoppingToken))
          {
            _logger.LogWarning("Failed to ensure latest version of desktop client.  Continuing optimistically.");
          }
          
          if (!await LaunchDesktopClient(session.SystemSessionId, stoppingToken))
          {
            _launchFailCount++;
          }
        }

        if (_launchFailCount < 10)
        {
          continue;
        }

        _logger.LogWarning(
          "Failed to launch desktop client in one or more sessions.  " +
          "Deleting existing desktop client installation to force a reinstall.");

        await DeleteDesktopClient();
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
      if (_environment.IsDebug)
      {
        _logger.LogDebug("Skipping desktop client deletion because we're in Debug mode.");
        return;
      }

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
        return await StartDebugSession();
      }

      var startupDir = _environment.StartupDirectory;
      var desktopDir = Path.Combine(startupDir, "DesktopClient");
      var binaryPath = Path.Combine(desktopDir, AppConstants.DesktopClientFileName);

      var result = _win32Interop.CreateInteractiveSystemProcess(
        $"\"{binaryPath}\" --instance-id {_settingsProvider.InstanceId}",
        sessionId,
        true,
        out var process);

      if (!result || process is null || process.Id == -1)
      {
        _logger.LogError("Failed to start desktop client process in session {SessionId}.", sessionId);
        return false;
      }

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

      return await _waiter.WaitFor(
        () => !process.HasExited && _ipcServerStore.ContainsServer(process.Id),
        TimeSpan.FromSeconds(1),
        throwOnCancellation: false,
        cancellationToken: linkedCts.Token);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while launching desktop client in session {SessionId}.", sessionId);
      return false;
    }
  }

  private async Task<bool> StartDebugSession()
  {
    var solutionDirResult = GetSolutionDir(Environment.CurrentDirectory);

    if (!solutionDirResult.IsSuccess)
    {
      return false;
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

    var psi = new ProcessStartInfo
    {
      WorkingDirectory = Path.GetDirectoryName(desktopClientPath),
      UseShellExecute = true,
      FileName = desktopClientPath,
      Arguments = "--instance-id localhost"
    };

    var process = _processManager.Start(psi);
    Guard.IsNotNull(process);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    return await _waiter.WaitFor(
      () => !process.HasExited && _ipcServerStore.ContainsServer(process.Id),
      TimeSpan.FromSeconds(1),
      throwOnCancellation: false,
      cancellationToken: cts.Token);
  }
}