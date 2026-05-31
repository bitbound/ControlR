using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Hosting;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Logging;

namespace ControlR.Agent.Common.Services.Windows;

[SupportedOSPlatform("windows8.0")]
internal class DesktopClientWatcherWin(
  TimeProvider timeProvider,
  IWin32Interop win32Interop,
  IProcessManager processManager,
  IIpcServerStore ipcServerStore,
  IDesktopClientRepairCoordinator desktopClientRepairCoordinator,
  ISystemEnvironment environment,
  IFileSystem fileSystem,
  IOptionsAccessor optionsAccessor,
  IDesktopSessionProvider desktopSessionProvider,
  IDesktopClientFileVerifier desktopClientFileVerifier,
  IDesktopClientLaunchTracker launchTracker,
  IWaiter waiter,
  IFileSystemPathProvider pathProvider,
  ILogger<DesktopClientWatcherWin> logger) : BackgroundService
{
  private readonly IDesktopClientFileVerifier _desktopClientFileVerifier = desktopClientFileVerifier;
  private readonly IDesktopClientRepairCoordinator _desktopClientRepairCoordinator = desktopClientRepairCoordinator;
  private readonly IDesktopSessionProvider _desktopSessionProvider = desktopSessionProvider;
  private readonly ISystemEnvironment _environment = environment;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IIpcServerStore _ipcServerStore = ipcServerStore;
  private readonly IDesktopClientLaunchTracker _launchTracker = launchTracker;
  private readonly ILogger<DesktopClientWatcherWin> _logger = logger;
  private readonly IOptionsAccessor _optionsAccessor = optionsAccessor;
  private readonly IFileSystemPathProvider _pathProvider = pathProvider;
  private readonly IProcessManager _processManager = processManager;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IWaiter _waiter = waiter;
  private readonly IWin32Interop _win32Interop = win32Interop;

  internal async Task RunIteration(
    IReadOnlyCollection<DesktopSession> activeSessions,
    DesktopSession[] desktopClients,
    CancellationToken stoppingToken)
  {
    var activeSessionIds = activeSessions
      .Select(x => x.SystemSessionId)
      .ToHashSet();

    _launchTracker.Reconcile(activeSessionIds, desktopClients);

    // Dispose of duplicate clients, those connected to the same session but not the "active" one.
    await DisposeDuplicateClients(desktopClients, stoppingToken);

    foreach (var session in activeSessions)
    {
      var repairKey = GetRepairSessionKey(session.SystemSessionId);

      if (desktopClients.Any(x => x.SystemSessionId == session.SystemSessionId))
      {
        _desktopClientRepairCoordinator.ReportHealthy(repairKey);
        continue;
      }

      if (_launchTracker.IsSessionCovered(session.SystemSessionId, desktopClients))
      {
        continue;
      }

      _logger.LogInformation(
        "No desktop client found in session {SessionId}. Launching a new one.",
        session.SystemSessionId);

      var refreshedDesktopClients = await _desktopSessionProvider.GetActiveDesktopClients();
      _launchTracker.Reconcile(activeSessionIds, refreshedDesktopClients);

      if (refreshedDesktopClients.Any(x => x.SystemSessionId == session.SystemSessionId))
      {
        _desktopClientRepairCoordinator.ReportHealthy(repairKey);
        continue;
      }

      if (_launchTracker.IsSessionCovered(session.SystemSessionId, refreshedDesktopClients))
      {
        continue;
      }

      var installationVerificationResult = VerifyDesktopClientInstallation();
      if (!installationVerificationResult.IsSuccess)
      {
        _logger.LogErrorDeduped(
          "Desktop client launch skipped because the installed desktop client is invalid. Reason: {Reason}",
          args: installationVerificationResult.Reason);
        _desktopClientRepairCoordinator.ReportFailure(
          "desktop-installation",
          installationVerificationResult.Reason ?? "Desktop client installation is invalid.",
          immediate: true);
        continue;
      }

      _desktopClientRepairCoordinator.ReportHealthy("desktop-installation");

      var launchSucceeded = await LaunchDesktopClient(session.SystemSessionId, stoppingToken);
      if (!launchSucceeded)
      {
        _desktopClientRepairCoordinator.ReportFailure(
          repairKey,
          $"Failed to launch desktop client in session {session.SystemSessionId}.");
      }
    }
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (_environment.IsDebug) 
    {
      _logger.LogInformation("Skipping DesktopClientWatcher in Debug mode.");
      return;
    }

    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), _timeProvider);

    try
    {
      while (await timer.WaitForNextTick(false, stoppingToken))
      {
        try
        {
          var activeSessions = _win32Interop.GetActiveSessions();
          var desktopClients = await _desktopSessionProvider.GetActiveDesktopClients();
          await RunIteration(activeSessions, desktopClients, stoppingToken);
        }
        catch (OperationCanceledException)
        {
          _logger.LogInformation("Stopping DesktopClientWatcher.  Application is shutting down.");
          break;
        }
        catch (Exception ex)
        {
          _logger.LogErrorDeduped("Error while checking for desktop client processes.", exception: ex);
        }
      }
    }
    finally
    {
      
      _launchTracker.Clear();
    }
  }

  private static string GetRepairSessionKey(int sessionId)
  {
    return $"windows-session-{sessionId}";
  }

  private static bool TryGetProcessSessionId(IProcess process, out int sessionId)
  {
    try
    {
      sessionId = process.SessionId;
      return true;
    }
    catch
    {
      sessionId = -1;
      return false;
    }
  }

  private async Task DisposeDuplicateClients(DesktopSession[] activeClients, CancellationToken cancellationToken)
  {
    try
    {
      // Get all IPC servers grouped by session ID
      var serversBySession = new Dictionary<int, List<IpcServerRecord>>();

      foreach (var serverRecord in _ipcServerStore.Servers.Values)
      {
        if (!TryGetProcessSessionId(serverRecord.Process, out var sessionId))
        {
          if (_ipcServerStore.TryRemove(serverRecord.Process.Id, out var removedRecord) && removedRecord is not null)
          {
            Disposer.DisposeAll(removedRecord.Process, removedRecord.Server);
          }

          continue;
        }

        if (!serversBySession.TryGetValue(sessionId, out var serversInSession))
        {
          serversInSession = [];
          serversBySession[sessionId] = serversInSession;
        }

        serversInSession.Add(serverRecord);
      }

      // For each session with active clients, find and dispose duplicates
      foreach (var activeClient in activeClients)
      {
        if (!serversBySession.TryGetValue(activeClient.SystemSessionId, out var serversInSession))
        {
          continue;
        }

        // Find duplicate servers (those with different PIDs than the active client)
        var duplicates = serversInSession
          .Where(s => s.Process.Id != activeClient.ProcessId)
          .ToList();

        if (duplicates.Count == 0)
        {
          continue;
        }

        _logger.LogWarning(
          "Found {DuplicateCount} duplicate desktop client(s) in session {SessionId}. " +
          "Active PID: {ActivePid}. Duplicate PIDs: {DuplicatePids}",
          duplicates.Count,
          activeClient.SystemSessionId,
          activeClient.ProcessId,
          string.Join(", ", duplicates.Select(d => d.Process.Id)));

        // Send shutdown command to each duplicate and remove from store
        foreach (var duplicate in duplicates)
        {
          try
          {
            _logger.LogInformation(
              "Shutting down duplicate desktop client. Session: {SessionId}, PID: {ProcessId}",
              activeClient.SystemSessionId,
              duplicate.Process.Id);

            var shutdownDto = new ShutdownCommandDto("Duplicate client detected");
            await duplicate.Server.Client.ShutdownDesktopClient(shutdownDto);

            // Remove from store
            _ipcServerStore.TryRemove(duplicate.Process.Id, out _);

            // Dispose the server and process
            Disposer.DisposeAll(duplicate.Process, duplicate.Server);
          }
          catch (Exception ex)
          {
            _logger.LogWarning(
              ex,
              "Failed to shutdown duplicate desktop client. Session: {SessionId}, PID: {ProcessId}",
              activeClient.SystemSessionId,
              duplicate.Process.Id);
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while disposing duplicate clients.");
    }
  }

  private async Task<bool> LaunchDesktopClient(int sessionId, CancellationToken cancellationToken)
  {
    IProcess? trackedProcess = null;

    try
    {
      if (_environment.IsDebug)
      {
        await StartDebugSession();
        return true;
      }

      var binaryPath = _pathProvider.GetDesktopExecutablePath();
      var launchCommand = string.IsNullOrWhiteSpace(_optionsAccessor.InstanceId)
        ? $"\"{binaryPath}\""
        : $"\"{binaryPath}\" --instance-id {_optionsAccessor.InstanceId}";

      var result = _win32Interop.CreateInteractiveSystemProcess(
        launchCommand,
        sessionId,
        true,
        out var process);

      if (!result || process is null || process.Id == -1)
      {
        _logger.LogError("Failed to start desktop client process in session {SessionId}.", sessionId);
        return false;
      }

      trackedProcess = process;
      _launchTracker.TrackLaunch(sessionId, trackedProcess);

      _logger.LogInformation(
        "Launched desktop client process for session {SessionId}. PID: {ProcessId}",
        sessionId,
        trackedProcess.Id);

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

      var registeredQuickly = await _waiter.WaitFor(
        () => trackedProcess.HasExited || _ipcServerStore.ContainsServer(trackedProcess.Id),
        TimeSpan.FromMilliseconds(250),
        throwOnCancellation: false,
        cancellationToken: linkedCts.Token);

      if (registeredQuickly && _ipcServerStore.ContainsServer(trackedProcess.Id))
      {
        _logger.LogInformation(
          "Desktop client for session {SessionId} registered with IPC shortly after launch. PID: {ProcessId}",
          sessionId,
          trackedProcess.Id);
        return true;
      }

      if (trackedProcess.HasExited)
      {
        if (_launchTracker.TryRemove(sessionId, trackedProcess.Id, out var removedState) &&
            removedState is not null)
        {
          removedState.Dispose();
        }

        _logger.LogWarning(
          "Desktop client process for session {SessionId} exited before IPC registration completed. PID: {ProcessId}",
          sessionId,
          trackedProcess.Id);
        return false;
      }

      _logger.LogInformation(
        "Desktop client process for session {SessionId} is still starting. PID: {ProcessId}. Waiting for IPC registration in the background.",
        sessionId,
        trackedProcess.Id);

      return true;
    }
    catch (Exception ex)
    {
      if (trackedProcess is not null &&
          _launchTracker.TryRemove(sessionId, trackedProcess.Id, out var removedState) &&
          removedState is not null)
      {
        removedState.Dispose();
      }

      _logger.LogErrorDeduped(
        "Error while launching desktop client in session {SessionId}. This error has been seen before.",
        args: sessionId,
        exception: ex);
      return false;
    }
  }

  private async Task StartDebugSession()
  {
    var solutionDirResult = IoHelper.GetSolutionDir(Environment.CurrentDirectory);

    if (!solutionDirResult.IsSuccess)
    {
      _logger.LogErrorDeduped(
        "Failed to find solution directory. Desktop client cannot be launched in debug mode. Reason: {Reason}",
        args: solutionDirResult.Reason);
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
    var waitResult = await _waiter.WaitFor(
      () => !process.HasExited && _ipcServerStore.ContainsServer(process.Id),
      TimeSpan.FromSeconds(1),
      throwOnCancellation: false,
      cancellationToken: cts.Token);

    if (!waitResult)
    {
      _logger.LogErrorDeduped(
        "Launched desktop client process in debug mode but it failed to register with IPC within the expected time. PID: {ProcessId}",
        args: process.Id);
    }
  }

  private Result VerifyDesktopClientInstallation()
  {
    var desktopExecutablePath = _pathProvider.GetDesktopExecutablePath();
    if (!_fileSystem.FileExists(desktopExecutablePath))
    {
      return Result.Fail($"Desktop client executable was not found at '{desktopExecutablePath}'.");
    }

    return _desktopClientFileVerifier.VerifyFile(desktopExecutablePath);
  }
}