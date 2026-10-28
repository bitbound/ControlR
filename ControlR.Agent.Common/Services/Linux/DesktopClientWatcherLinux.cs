using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Logging;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Globalization;

namespace ControlR.Agent.Common.Services.Linux;

internal class DesktopClientWatcherLinux(
  TimeProvider timeProvider,
  IServiceControl serviceControl,
  IDesktopClientRepairCoordinator desktopClientRepairCoordinator,
  IDesktopEnvironmentDetectorAgent desktopEnvironmentDetector,
  IHeadlessServerDetector headlessServerDetector,
  ISystemEnvironment systemEnvironment,
  IFileSystem fileSystem,
  IDesktopClientFileVerifier desktopClientFileVerifier,
  ILoggedInUserProvider loggedInUserProvider,
  IProcessManager processManager,
  IFileSystemPathProvider fileSystemPathProvider,
  ILinuxDesktopClientProcessLocator processLocator,
  ILogger<DesktopClientWatcherLinux> logger) : BackgroundService
{
  private const int ProcessCommandTimeoutMs = 5_000;

  private static readonly TimeSpan _maxLoginScreenLaunchBackoff = TimeSpan.FromMinutes(5);

  private readonly IDesktopClientFileVerifier _desktopClientFileVerifier = desktopClientFileVerifier;
  private readonly IDesktopClientRepairCoordinator _desktopClientRepairCoordinator = desktopClientRepairCoordinator;
  private readonly IDesktopEnvironmentDetectorAgent _desktopEnvironmentDetector = desktopEnvironmentDetector;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IFileSystemPathProvider _fileSystemPathProvider = fileSystemPathProvider;
  private readonly IHeadlessServerDetector _headlessServerDetector = headlessServerDetector;
  private readonly ILoggedInUserProvider _loggedInUserProvider = loggedInUserProvider;
  private readonly ILogger<DesktopClientWatcherLinux> _logger = logger;
  private readonly ILinuxDesktopClientProcessLocator _processLocator = processLocator;
  private readonly IProcessManager _processManager = processManager;
  private readonly IServiceControl _serviceControl = serviceControl;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;

  private int _loginScreenLaunchFailures;
  private DateTimeOffset _loginScreenLaunchNotBefore = DateTimeOffset.MinValue;
  private IProcess? _loginScreenProcess;
  private int _loginScreenStopRequests;

  internal async Task CheckLoginScreenDesktopClient(CancellationToken cancellationToken)
  {
    var displayInfo = await _desktopEnvironmentDetector.DetectDisplayEnvironment();

    if (!displayInfo.IsConclusive)
    {
      // logind could not be queried, so we cannot tell whether this host is at a login
      // screen. Leave any running client alone rather than tearing down a healthy session.
      _logger.LogWarningDeduped("Login screen detection was inconclusive. Leaving the desktop client state unchanged.");
      return;
    }

    if (displayInfo.IsWayland)
    {
      _logger.LogInformationDeduped(
        "Wayland login screen detected. Desktop client launch is not supported in the greeter session. Display: {WaylandDisplay}, DM: {DisplayManager}",
        args: [displayInfo.WaylandDisplay ?? "wayland-0", displayInfo.DisplayManager ?? "unknown"]);

      return;
    }

    if (!displayInfo.IsLoginScreen)
    {
      // If not at the login screen, ensure the login screen client is stopped. Reconcile first, so an
      // untracked root-owned client does not survive the greeter into the user's session, where no
      // later greeter pass would run to reap it.
      ResetLoginScreenLaunchBackoff();
      ReconcileGreeterDesktopClients();
      await StopLoginScreenDesktopClient();
      _desktopClientRepairCoordinator.ReportHealthy("linux-login-screen");
      return;
    }

    _logger.LogInformationDeduped(
      "Login screen detected. Type: X11, Display: {Display}, XAuth: {XAuth}, DM: {DisplayManager}",
      args: [displayInfo.Display, displayInfo.XAuthPath ?? "none", displayInfo.DisplayManager ?? "unknown"]);

    // Reconcile on every pass, not just the ones that end in a launch. A client this agent did not
    // record can appear beside the one it did, and the launch path is not reached while the recorded
    // client still looks healthy or while a failing launch is backing off.
    ReconcileGreeterDesktopClients();

    var isLoginScreenRunning = await IsLoginScreenDesktopClientRunning();
    if (isLoginScreenRunning && await HasDeletedLoginScreenDesktopClientExecutable())
    {
      _logger.LogWarningDeduped(
        "Login screen desktop client is running from a deleted executable. Restarting it.");

      await StopLoginScreenDesktopClient();
      isLoginScreenRunning = false;
    }

    // Launch the desktop client for the login screen if needed
    if (!isLoginScreenRunning)
    {
      var now = _timeProvider.GetUtcNow();
      if (now < _loginScreenLaunchNotBefore)
      {
        _logger.LogDebugDeduped(
          "Login screen desktop client launch is delayed until {NotBefore}.",
          args: [_loginScreenLaunchNotBefore]);
        return;
      }

      if (string.IsNullOrWhiteSpace(displayInfo.XAuthPath))
      {
        // Without the greeter's auth cookie the X11 client starts and exits immediately.
        _logger.LogWarningDeduped(
          "X11 login screen detected without an XAUTHORITY file. Skipping desktop client launch. Display: {Display}, DM: {DisplayManager}",
          args: [displayInfo.Display, displayInfo.DisplayManager ?? "unknown"]);
        return;
      }

      _ = LaunchLoginScreenDesktopClient(displayInfo, cancellationToken);
      return;
    }

    _desktopClientRepairCoordinator.ReportHealthy("linux-login-screen");
  }

  /// <summary>
  /// Kills login screen desktop clients this agent is not tracking, so the greeter session holds at
  /// most one. A client left behind by a previous agent instance, or by a launch attempt that lost the
  /// race to record itself, is not in _loginScreenProcess, so it is found by the executable it runs
  /// rather than by this agent's own record of it.
  /// </summary>
  internal void ReconcileGreeterDesktopClients()
  {
    var expectedPath = GetLoginScreenDesktopClientPath();

    int? trackedProcessId = null;
    try
    {
      trackedProcessId = _loginScreenProcess?.Id;
    }
    catch (Exception)
    {
      // The launch task disposes the process concurrently. Treat it as untracked.
    }

    foreach (var process in _processLocator.FindByExecutablePath(expectedPath))
    {
      // The agent spawns the login screen client as root. A user session client runs under its own
      // uid and belongs to the user's systemd unit, so it is never touched here.
      if (process.OwnerUid != 0)
      {
        continue;
      }

      if (process.ProcessId == trackedProcessId)
      {
        continue;
      }

      KillStaleGreeterClient(process.ProcessId, expectedPath);
    }
  }

  internal async Task StopClientsForShutdownAsync()
  {
    await StopLoginScreenDesktopClient();
    await _serviceControl.StopDesktopClientService(throwOnFailure: false);
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (_systemEnvironment.IsDebug)
    {
      _logger.LogInformation("Desktop process watcher is running in debug mode. Skipping watch.");
      return;
    }

    // A previous agent instance can leave a login screen client running, because an update replaces
    // the binary out from under it and, before this reconcile existed, nothing killed it on the way
    // down. This agent did not spawn that process, so it is not in _loginScreenProcess. Reap any such
    // orphan once at startup, before this agent spawns a client of its own beside it.
    try
    {
      ReconcileGreeterDesktopClients();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error reconciling stale login screen desktop clients at startup.");
    }

    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), _timeProvider);
    while (await timer.WaitForNextTick(throwOnCancellation: false, stoppingToken))
    {
      try
      {
        var isHeadless = await _headlessServerDetector.IsHeadlessServer();
        if (isHeadless)
        {
          _logger.LogInformationDeduped( "Running on headless Linux server. Desktop client services are not applicable.");
          await Task.Delay(TimeSpan.FromMinutes(1), _timeProvider, stoppingToken);
          continue;
        }

        await CheckAndStartDesktopClientServices(stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while checking for desktop processes.");
      }
    }

    // This agent owns the login screen client it spawned, so it takes it down on the way out. Leaving
    // it running orphans a process whose binary the next update is about to replace.
    await StopClientsForShutdownAsync();
  }

  private static string GetRepairKey(string uid)
  {
    return $"linux-user-{uid}";
  }

  private async Task CheckAndStartDesktopClientServices(CancellationToken cancellationToken)
  {
    try
    {
      // Check for the login screen first
      await CheckLoginScreenDesktopClient(cancellationToken);

      // Then check for logged-in users
      var loggedInUsers = (await _loggedInUserProvider.GetLoggedInUsers())
          .Where(user => user.IsActive)
          .Select(user => user.Uid)
          .ToList();
      if (loggedInUsers.Count == 0)
      {
        _logger.LogDebugDeduped( "No logged-in users found.");
        return;
      }

      _logger.LogDebugDeduped("Found {UserCount} logged-in users.", args: loggedInUsers.Count);

      foreach (var uid in loggedInUsers)
      {
        await CheckDesktopClientForUser(uid, cancellationToken);
      }
    }
    catch (Exception ex)
    {
      _logger.LogErrorDeduped("Error checking desktop client services.", exception: ex);
    }
  }

  private async Task CheckDesktopClientForUser(string uid, CancellationToken cancellationToken)
  {
    try
    {
      var installationValidation = ValidateDesktopClientInstallation();
      if (!installationValidation.IsSuccess)
      {
        _desktopClientRepairCoordinator.ReportFailure(
          "desktop-installation",
          installationValidation.Reason ?? "Desktop client installation is invalid.",
          immediate: true);
        return;
      }

      var serviceName = GetDesktopClientServiceName();
      var isRunning = await IsDesktopClientServiceRunning(uid, serviceName);
      var repairKey = GetRepairKey(uid);

      cancellationToken.ThrowIfCancellationRequested();

      if (!isRunning)
      {
        _logger.LogInformationDeduped("Desktop client service not running for user {UID}. Starting service.", args: uid);
        await StartDesktopClientServiceForUser(uid, serviceName, cancellationToken);
        return;
      }

      if (await HasDeletedDesktopClientExecutable(uid, serviceName))
      {
        _logger.LogWarningDeduped("Desktop client service for user {UID} is running from a deleted executable. Restarting service.", args: uid);
        await RestartDesktopClientServiceForUser(uid, serviceName, cancellationToken);
        return;
      }

      _desktopClientRepairCoordinator.ReportHealthy(repairKey);
      _logger.LogDebugDeduped("Desktop client service is running for user {UID}.", args: uid);
    }
    catch (Exception ex)
    {
      _desktopClientRepairCoordinator.ReportFailure(
        GetRepairKey(uid),
        $"Failed to check or start the desktop client for user {uid}: {ex.Message}");
      _logger.LogWarningDeduped("Failed to check/start desktop client service for user {UID}.", args: uid, exception: ex);
    }
  }

  private async Task<int?> GetDesktopClientMainProcessId(string uid, string serviceName)
  {
    try
    {
      var result = await _processManager.GetProcessOutput(
        "sudo",
        $"-u #{uid} XDG_RUNTIME_DIR=/run/user/{uid} systemctl --user show {serviceName} --property MainPID --value",
        ProcessCommandTimeoutMs);

      if (!result.IsSuccess)
      {
        _logger.LogWarningDeduped( "Failed to resolve main PID for desktop service {ServiceName} and user {UID}: {Reason}", args: [serviceName, uid, result.Reason]);
        return null;
      }

      var output = result.Value?.Trim();
      if (!int.TryParse(output, out var pid) || pid <= 0)
      {
        return null;
      }

      return pid;
    }
    catch (Exception ex)
    {
      _logger.LogWarningDeduped( "Failed to read main PID for desktop service {ServiceName} and user {UID}.", args: [serviceName, uid], exception: ex);
      return null;
    }
  }

  private string GetDesktopClientServiceName()
  {
    var instanceId = _fileSystemPathProvider.GetEffectiveInstanceId();
    return string.Equals(instanceId, AppConstants.DefaultInstanceId, StringComparison.Ordinal)
      ? BrandingConstants.LinuxDesktopServiceName
      : $"{BrandingConstants.LinuxDesktopServiceName.Replace(".service", $"-{instanceId}.service")}";
  }

  private string GetInstallDirectory()
  {
    return _fileSystemPathProvider.GetAgentInstallDirectory();
  }

  private string GetLoginScreenDesktopClientPath()
  {
    return Path.Combine(
      GetInstallDirectory(),
      BrandingConstants.DesktopClientDirectoryName,
      AppConstants.DesktopClientFileName);
  }

  private TimeSpan GetLoginScreenLaunchBackoff()
  {
    var seconds = Math.Min(
      _maxLoginScreenLaunchBackoff.TotalSeconds,
      5 * Math.Pow(2, _loginScreenLaunchFailures));

    return TimeSpan.FromSeconds(seconds);
  }

  private async Task<bool> HasDeletedDesktopClientExecutable(string uid, string serviceName)
  {
    var processId = await GetDesktopClientMainProcessId(uid, serviceName);
    if (processId is null)
    {
      return false;
    }

    return await IsDeletedExecutable(processId.Value);
  }

  private async Task<bool> HasDeletedLoginScreenDesktopClientExecutable()
  {
    if (_loginScreenProcess is null)
    {
      return false;
    }

    return await IsDeletedExecutable(_loginScreenProcess.Id);
  }

  private async Task<bool> IsDeletedExecutable(int processId)
  {
    try
    {
      var result = await _processManager.GetProcessOutput("readlink", $"/proc/{processId}/exe", ProcessCommandTimeoutMs);
      if (!result.IsSuccess)
      {
        return false;
      }

      var executablePath = result.Value?.Trim();
      return executablePath?.EndsWith(" (deleted)", StringComparison.Ordinal) == true;
    }
    catch (Exception ex)
    {
      _logger.LogWarningDeduped( "Failed to inspect executable path for PID {PID}.", args: processId, exception: ex);
      return false;
    }
  }

  private async Task<bool> IsDesktopClientServiceRunning(string uid, string serviceName)
  {
    try
    {
      // Use systemctl to check whether the user service is running
      // Set the XDG_RUNTIME_DIR for the user context
      var result = await _processManager.GetProcessOutput("sudo", $"-u #{uid} XDG_RUNTIME_DIR=/run/user/{uid} systemctl --user is-active {serviceName}", ProcessCommandTimeoutMs);

      if (!result.IsSuccess)
      {
        _logger.LogWarningDeduped("Service {ServiceName} not found for user {UID}: {Reason}", args: [serviceName, uid, result.Reason]);
        return false;
      }

      // The systemctl is-active command returns "active" if the service is running
      var output = result.Value?.Trim();
      var isRunning = string.Equals(output, "active", StringComparison.OrdinalIgnoreCase);

      _logger.LogDebugDeduped("Service {ServiceName} status for user {UID}: {Status}", args: [serviceName, uid, isRunning ? "Running" : "Not running"]);

      return isRunning;
    }
    catch (Exception ex)
    {
      _logger.LogWarningDeduped("Failed to check service status for user {UID}.", args: uid, exception: ex);
      return false;
    }
  }

  private Task<bool> IsLoginScreenDesktopClientRunning()
  {
    try
    {
      return Task.FromResult(_loginScreenProcess is { HasExited: false });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking login screen desktop client status");
      return Task.FromResult(false);
    }
  }

  private void KillStaleGreeterClient(int processId, string expectedPath)
  {
    // Re-check the executable and its owner immediately before killing. A pid found earlier can have
    // been recycled to an unrelated process, or to a user session client of the same binary, and
    // neither is ours to kill. An unreadable owner also lands here, so the kill stays fail closed.
    if (_processLocator.GetRunningExecutableOwnerUid(processId, expectedPath) != 0)
    {
      return;
    }

    try
    {
      _processManager.GetProcessById(processId).KillAndDispose();
      _logger.LogInformation(
        "Killed a stale login screen desktop client this agent is not tracking. PID: {ProcessId}",
        processId);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to kill a stale login screen desktop client. PID: {ProcessId}", processId);
    }
  }

  private async Task LaunchLoginScreenDesktopClient(DisplayEnvironmentInfo displayInfo, CancellationToken cancellationToken)
  {
    var launchStartedAt = _timeProvider.GetUtcNow();
    var stopRequestsAtLaunch = Volatile.Read(ref _loginScreenStopRequests);
    var processHandedOff = false;

    try
    {
      var desktopClientPath = GetLoginScreenDesktopClientPath();

      if (!_fileSystem.FileExists(desktopClientPath))
      {
        _desktopClientRepairCoordinator.ReportFailure(
          "desktop-installation",
          $"Desktop client executable was not found at '{desktopClientPath}'.",
          immediate: true);
        _logger.LogError("Desktop client executable not found at {Path}", desktopClientPath);
        return;
      }

      var instanceId = _fileSystemPathProvider.GetEffectiveInstanceId();
      var instanceArgs = string.Equals(instanceId, AppConstants.DefaultInstanceId, StringComparison.Ordinal)
        ? ""
        : $" --instance-id {instanceId}";

      if (displayInfo.IsWayland)
      {
        _logger.LogInformationDeduped("Skipping login screen desktop client launch for Wayland greeter session.");
        return;
      }

      // Set up the environment variables for the X11 login screen session
      var envVars = new Dictionary<string, string>
      {
        ["DOTNET_ENVIRONMENT"] = "Production",
        ["DISPLAY"] = displayInfo.Display,
        ["XDG_SESSION_TYPE"] = "x11",
        // Names this agent to the client. On Linux a child outlives its parent, so without this a
        // client left behind by an agent that updated or died keeps the greeter display to itself.
        [AppConstants.AgentPidEnvironmentVariable] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
      };

      if (!string.IsNullOrEmpty(displayInfo.XAuthPath))
      {
        envVars["XAUTHORITY"] = displayInfo.XAuthPath;
      }

      _logger.LogInformationDeduped(
        "Starting login screen desktop client. Type: X11, Display: {Display}, XAUTH: {XAuth}",
        args: [displayInfo.Display, displayInfo.XAuthPath ?? "none"]);

      // Start the process with the proper environment
      var startInfo = new ProcessStartInfo
      {
        FileName = desktopClientPath,
        Arguments = instanceArgs.Trim(),
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = Path.GetDirectoryName(desktopClientPath)
      };

      foreach (var kvp in envVars)
      {
        startInfo.Environment[kvp.Key] = kvp.Value;
      }

      var process = _processManager.Start(startInfo);

      if (process is null)
      {
        RecordLoginScreenLaunchExit(launchStartedAt);
        _desktopClientRepairCoordinator.ReportFailure("linux-login-screen", "Failed to start the login screen desktop client process.");
        _logger.LogError("Failed to start login screen desktop client process");
        return;
      }

      _loginScreenProcess = process;
      processHandedOff = true;

      try
      {
        _logger.LogInformationDeduped("Login screen desktop client started.");
        await process.WaitForExitAsync(cancellationToken);
        _logger.LogInformationDeduped("Login screen desktop client exited.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error waiting for login screen desktop client process");
      }
      finally
      {
        if (ReferenceEquals(_loginScreenProcess, process))
        {
          _loginScreenProcess = null;
        }

        var stoppedIntentionally = Volatile.Read(ref _loginScreenStopRequests) != stopRequestsAtLaunch;
        process.KillAndDispose();

        if (!stoppedIntentionally)
        {
          RecordLoginScreenLaunchExit(launchStartedAt);
        }
      }
    }
    catch (Exception ex)
    {
      if (!processHandedOff)
      {
        RecordLoginScreenLaunchExit(launchStartedAt);
      }

      _logger.LogError(ex, "Error launching login screen desktop client");
      _desktopClientRepairCoordinator.ReportFailure("linux-login-screen", $"Error launching the login screen desktop client: {ex.Message}");
    }
  }

  private void RecordLoginScreenLaunchExit(DateTimeOffset launchStartedAt)
  {
    if (_timeProvider.GetUtcNow() - launchStartedAt >= TimeSpan.FromMinutes(1))
    {
      _loginScreenLaunchFailures = 0;
      return;
    }

    _loginScreenLaunchFailures++;
    _loginScreenLaunchNotBefore = _timeProvider.GetUtcNow() + GetLoginScreenLaunchBackoff();
  }

  private void ResetLoginScreenLaunchBackoff()
  {
    _loginScreenLaunchFailures = 0;
    _loginScreenLaunchNotBefore = DateTimeOffset.MinValue;
  }

  private async Task RestartDesktopClientServiceForUser(string uid, string serviceName, CancellationToken cancellationToken)
  {
    await RunDesktopClientServiceCommandForUser(uid, serviceName, "restart", cancellationToken);
  }

  private async Task RunDesktopClientServiceCommandForUser(string uid, string serviceName, string command, CancellationToken cancellationToken)
  {
    var exitCode = await _processManager.StartAndWaitForExit(
      "sudo",
      $"-u #{uid} XDG_RUNTIME_DIR=/run/user/{uid} systemctl --user {command} {serviceName}",
      false,
      cancellationToken);

    if (exitCode != 0)
    {
      throw new InvalidOperationException($"systemctl --user {command} {serviceName} failed for UID {uid} with exit code {exitCode}.");
    }
  }

  private Task StartDesktopClientServiceForUser(string uid, string serviceName, CancellationToken cancellationToken)
  {
    return RunDesktopClientServiceCommandForUser(uid, serviceName, "start", cancellationToken);
  }

  private Task StopLoginScreenDesktopClient()
  {
    try
    {
      if (_loginScreenProcess is null)
      {
        return Task.CompletedTask;
      }

      Interlocked.Increment(ref _loginScreenStopRequests);
      _logger.LogInformation("Stopping login screen desktop client (PID {PID})", _loginScreenProcess.Id);
      _loginScreenProcess.KillAndDispose();
      _loginScreenProcess = null;
      _logger.LogInformation("Login screen desktop client stopped");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error stopping login screen desktop client");
    }

    return Task.CompletedTask;
  }

  private Result ValidateDesktopClientInstallation()
  {
    var desktopClientPath = Path.Combine(GetInstallDirectory(), "DesktopClient", AppConstants.DesktopClientFileName);
    if (!_fileSystem.FileExists(desktopClientPath))
    {
      return Result.Fail($"Desktop client executable was not found at '{desktopClientPath}'.");
    }

    var verificationResult = _desktopClientFileVerifier.VerifyFile(desktopClientPath);
    if (verificationResult.IsSuccess)
    {
      _desktopClientRepairCoordinator.ReportHealthy("desktop-installation");
      return verificationResult;
    }

    return verificationResult;
  }
}
