using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Shared.Services;
using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Logging;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

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
  ILogger<DesktopClientWatcherLinux> logger) : BackgroundService
{
  private const int ProcessCommandTimeoutMs = 5_000;

  private readonly IDesktopClientFileVerifier _desktopClientFileVerifier = desktopClientFileVerifier;
  private readonly IDesktopClientRepairCoordinator _desktopClientRepairCoordinator = desktopClientRepairCoordinator;
  private readonly IDesktopEnvironmentDetectorAgent _desktopEnvironmentDetector = desktopEnvironmentDetector;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IFileSystemPathProvider _fileSystemPathProvider = fileSystemPathProvider;
  private readonly IHeadlessServerDetector _headlessServerDetector = headlessServerDetector;
  private readonly ILoggedInUserProvider _loggedInUserProvider = loggedInUserProvider;
  private readonly ILogger<DesktopClientWatcherLinux> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  private readonly IServiceControl _serviceControl = serviceControl;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;

  private IProcess? _loginScreenProcess;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (_systemEnvironment.IsDebug)
    {
      _logger.LogInformation("Desktop process watcher is running in debug mode. Skipping watch.");
      return;
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

    
    await _serviceControl.StopDesktopClientService(throwOnFailure: false);
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
      var loggedInUsers = await _loggedInUserProvider.GetLoggedInUserUids();
      if (loggedInUsers.Count == 0)
      {
        _logger.LogInformationDeduped( "No logged-in users found.");
        return;
      }

      _logger.LogInformationDeduped("Found {UserCount} logged-in users.", args: loggedInUsers.Count);

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
      _logger.LogInformationDeduped("Desktop client service is running for user {UID}.", args: uid);
    }
    catch (Exception ex)
    {
      _desktopClientRepairCoordinator.ReportFailure(
        GetRepairKey(uid),
        $"Failed to check or start the desktop client for user {uid}: {ex.Message}");
      _logger.LogWarningDeduped("Failed to check/start desktop client service for user {UID}.", args: uid, exception: ex);
    }
  }

  private async Task CheckLoginScreenDesktopClient(CancellationToken cancellationToken)
  {
    var displayInfo = await _desktopEnvironmentDetector.DetectDisplayEnvironment();

    if (displayInfo.IsWayland)
    {
      _logger.LogInformationDeduped(
        "Wayland login screen detected. Desktop client launch is not supported in the greeter session. Display: {WaylandDisplay}, DM: {DisplayManager}",
        args: [displayInfo.WaylandDisplay ?? "wayland-0", displayInfo.DisplayManager ?? "unknown"]);

      return;
    }

    if (!displayInfo.IsLoginScreen)
    {
      // If not at the login screen, ensure the login screen client is stopped
      await StopLoginScreenDesktopClient();
      _desktopClientRepairCoordinator.ReportHealthy("linux-login-screen");
      return;
    }

    _logger.LogInformationDeduped(
      "Login screen detected. Type: X11, Display: {Display}, XAuth: {XAuth}, DM: {DisplayManager}",
      args: [displayInfo.Display, displayInfo.XAuthPath ?? "none", displayInfo.DisplayManager ?? "unknown"]);

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
      _ = LaunchLoginScreenDesktopClient(displayInfo, cancellationToken);
      return;
    }

    _desktopClientRepairCoordinator.ReportHealthy("linux-login-screen");
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
    return string.Equals(instanceId, AppConstants.DefaultInstallDirectoryName, StringComparison.Ordinal)
      ? "controlr.desktop.service"
      : $"controlr.desktop-{instanceId}.service";
  }

  private string GetInstallDirectory()
  {
    return _fileSystemPathProvider.GetAgentInstallDirectory();
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

      _logger.LogInformationDeduped("Service {ServiceName} status for user {UID}: {Status}", args: [serviceName, uid, isRunning ? "Running" : "Not running"]);

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

  private async Task LaunchLoginScreenDesktopClient(DisplayEnvironmentInfo displayInfo, CancellationToken cancellationToken)
  {
    try
    {
      var installDir = GetInstallDirectory();
      var desktopClientPath = Path.Combine(installDir, "DesktopClient", "ControlR.DesktopClient");

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
      var instanceArgs = string.Equals(instanceId, AppConstants.DefaultInstallDirectoryName, StringComparison.Ordinal)
        ? ""
        : $" --instance-id {instanceId}";

      if (displayInfo.IsWayland)
      {
        _logger.LogInformation("Skipping login screen desktop client launch for Wayland greeter session.");
        return;
      }

      // Set up the environment variables for the X11 login screen session
      var envVars = new Dictionary<string, string>
      {
        ["DOTNET_ENVIRONMENT"] = "Production",
        ["DISPLAY"] = displayInfo.Display,
        ["XDG_SESSION_TYPE"] = "x11"
      };

      if (!string.IsNullOrEmpty(displayInfo.XAuthPath))
      {
        envVars["XAUTHORITY"] = displayInfo.XAuthPath;
      }

      _logger.LogInformation("Starting login screen desktop client. Type: X11, Display: {Display}, XAUTH: {XAuth}",
        displayInfo.Display, displayInfo.XAuthPath ?? "none");

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

      _loginScreenProcess = _processManager.Start(startInfo);

      if (_loginScreenProcess is null)
      {
        _desktopClientRepairCoordinator.ReportFailure("linux-login-screen", "Failed to start the login screen desktop client process.");
        _logger.LogError("Failed to start login screen desktop client process");
        return;
      }

      try
      {
        _logger.LogInformation("Login screen desktop client started with PID {PID}", _loginScreenProcess.Id);
        await _loginScreenProcess.WaitForExitAsync(cancellationToken);
        _logger.LogInformation("Login screen desktop client (PID {PID}) has exited", _loginScreenProcess.Id);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error waiting for login screen desktop client process");
      }
      finally
      {
        _loginScreenProcess.KillAndDispose();
        _loginScreenProcess = null;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error launching login screen desktop client");
      _desktopClientRepairCoordinator.ReportFailure("linux-login-screen", $"Error launching the login screen desktop client: {ex.Message}");
    }
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
