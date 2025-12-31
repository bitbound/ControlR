using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ControlR.Agent.Common.Services.Linux;

internal class DesktopClientWatcherLinux(
  TimeProvider timeProvider,
  IServiceControl serviceControl,
  IDesktopEnvironmentDetectorAgent desktopEnvironmentDetector,
  IHeadlessServerDetector headlessServerDetector,
  ISystemEnvironment systemEnvironment,
  IFileSystem fileSystem,
  IControlrMutationLock mutationLock,
  ILoggedInUserProvider loggedInUserProvider,
  IProcessManager processManager,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<DesktopClientWatcherLinux> logger) : BackgroundService
{
  private readonly IDesktopEnvironmentDetectorAgent _desktopEnvironmentDetector = desktopEnvironmentDetector;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IHeadlessServerDetector _headlessServerDetector = headlessServerDetector;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILoggedInUserProvider _loggedInUserProvider = loggedInUserProvider;
  private readonly ILogger<DesktopClientWatcherLinux> _logger = logger;
  private readonly IControlrMutationLock _mutationLock = mutationLock;
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
        using var dedupeScope = _logger.EnterDedupeScope();
        var isHeadless = await _headlessServerDetector.IsHeadlessServer();
        if (isHeadless)
        {
          _logger.LogDeduped(LogLevel.Information, "Running on headless Ubuntu server. Desktop client services are not applicable.");
          await Task.Delay(TimeSpan.FromMinutes(1), _timeProvider, stoppingToken);
          continue;
        }

        using var mutationLock = await _mutationLock.AcquireAsync(stoppingToken);
        await CheckAndStartDesktopClientServices(stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while checking for desktop processes.");
      }
    }

    await _serviceControl.StopDesktopClientService(throwOnFailure: false);
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
        _logger.LogDeduped(LogLevel.Information, "No logged-in users found.");
        return;
      }

      _logger.LogDeduped(LogLevel.Information, "Found {UserCount} logged-in users.", args: loggedInUsers.Count);

      foreach (var uid in loggedInUsers)
      {
        await CheckDesktopClientForUser(uid, cancellationToken);
      }
    }
    catch (Exception ex)
    {
      _logger.LogDeduped(LogLevel.Error, "Error checking desktop client services.", exception: ex);
    }
  }

  private async Task CheckDesktopClientForUser(string uid, CancellationToken cancellationToken)
  {
    try
    {
      var serviceName = GetDesktopClientServiceName();
      var isRunning = await IsDesktopClientServiceRunning(uid, serviceName);

      cancellationToken.ThrowIfCancellationRequested();

      if (!isRunning)
      {
        _logger.LogDeduped(LogLevel.Information, "Desktop client service not running for user {UID}. Starting service.", args: uid);
        await _serviceControl.StartDesktopClientService(throwOnFailure: true);
      }
      else
      {
        _logger.LogDeduped(LogLevel.Information, "Desktop client service is running for user {UID}.", args: uid);
      }
    }
    catch (Exception ex)
    {
      _logger.LogDeduped(LogLevel.Warning, "Failed to check/start desktop client service for user {UID}.", args: uid, exception: ex);
    }
  }

  private async Task CheckLoginScreenDesktopClient(CancellationToken cancellationToken)
  {
    var displayInfo = await _desktopEnvironmentDetector.DetectDisplayEnvironment();
    if (!displayInfo.IsLoginScreen)
    {
      // If not at the login screen, ensure the login screen client is stopped
      await StopLoginScreenDesktopClient();
      return;
    }

    if (displayInfo.IsWayland)
    {
      _logger.LogDeduped(
        LogLevel.Information,
        "Login screen detected. Type: Wayland, Display: {WaylandDisplay}, DM: {DisplayManager}",
        args: (displayInfo.WaylandDisplay ?? "wayland-0", displayInfo.DisplayManager ?? "unknown"));
    }
    else
    {
      _logger.LogDeduped(
        LogLevel.Information,
        "Login screen detected. Type: X11, Display: {Display}, XAuth: {XAuth}, DM: {DisplayManager}",
        args: (displayInfo.Display, displayInfo.XAuthPath ?? "none", displayInfo.DisplayManager ?? "unknown"));
    }

    // Launch the desktop client for the login screen if needed
    if (!await IsLoginScreenDesktopClientRunning())
    {
      _ = LaunchLoginScreenDesktopClient(displayInfo, cancellationToken);
    }
  }

  private string GetDesktopClientServiceName()
  {
    // This should match the service name from the ServiceControlLinux
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "controlr.desktop.service";
    }

    return $"controlr.desktop-{_instanceOptions.Value.InstanceId}.service";
  }

  private string GetInstallDirectory()
  {
    var dir = "/usr/local/bin/ControlR";
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return dir;
    }

    return Path.Combine(dir, _instanceOptions.Value.InstanceId);
  }

  private async Task<bool> IsDesktopClientServiceRunning(string uid, string serviceName)
  {
    try
    {
      // Use systemctl to check whether the user service is running
      // Set the XDG_RUNTIME_DIR for the user context
      var result = await _processManager.GetProcessOutput("sudo", $"-u #{uid} XDG_RUNTIME_DIR=/run/user/{uid} systemctl --user is-active {serviceName}", 5000);

      if (!result.IsSuccess)
      {
        _logger.LogDeduped(LogLevel.Warning, "Service {ServiceName} not found for user {UID}: {Reason}", args: (serviceName, uid, result.Reason));
        return false;
      }

      // The systemctl is-active command returns "active" if the service is running
      var output = result.Value?.Trim();
      var isRunning = string.Equals(output, "active", StringComparison.OrdinalIgnoreCase);

      _logger.LogDeduped(LogLevel.Information, "Service {ServiceName} status for user {UID}: {Status}", args: (serviceName, uid, isRunning ? "Running" : "Not running"));

      return isRunning;
    }
    catch (Exception ex)
    {
      _logger.LogDeduped(LogLevel.Warning, "Failed to check service status for user {UID}.", args: uid, exception: ex);
      return false;
    }
  }

  private async Task<bool> IsLoginScreenDesktopClientRunning()
  {
    try
    {
      return _loginScreenProcess is { HasExited: false };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking login screen desktop client status");
      return false;
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
        _logger.LogError("Desktop client executable not found at {Path}", desktopClientPath);
        return;
      }

      var instanceArgs = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
        ? ""
        : $" --instance-id {_instanceOptions.Value.InstanceId}";

      // Set up the environment variables based on session type
      var envVars = new Dictionary<string, string>
      {
        ["DOTNET_ENVIRONMENT"] = "Production"
      };

      if (displayInfo.IsWayland)
      {
        // Configure for Wayland session
        envVars[AppConstants.WaylandLoginScreenVariable] = "true";
        envVars["XDG_SESSION_TYPE"] = "wayland";
        envVars["WAYLAND_DISPLAY"] = displayInfo.WaylandDisplay ?? "wayland-0";

        // Set XDG_RUNTIME_DIR to the greeter's runtime directory
        if (!string.IsNullOrEmpty(displayInfo.WaylandRuntimeDir))
        {
          envVars["XDG_RUNTIME_DIR"] = displayInfo.WaylandRuntimeDir;
        }

        _logger.LogInformation("Starting login screen desktop client. Type: Wayland, Display: {WaylandDisplay}, RuntimeDir: {RuntimeDir}",
          displayInfo.WaylandDisplay ?? "wayland-0", displayInfo.WaylandRuntimeDir ?? "unknown");
      }
      else
      {
        // Configure for X11 session
        envVars["DISPLAY"] = displayInfo.Display;
        envVars["XDG_SESSION_TYPE"] = "x11";

        if (!string.IsNullOrEmpty(displayInfo.XAuthPath))
        {
          envVars["XAUTHORITY"] = displayInfo.XAuthPath;
        }

        _logger.LogInformation("Starting login screen desktop client. Type: X11, Display: {Display}, XAUTH: {XAuth}",
          displayInfo.Display, displayInfo.XAuthPath ?? "none");
      }

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
    }
  }

  private async Task StopLoginScreenDesktopClient()
  {
    try
    {
      if (_loginScreenProcess is null)
      {
        return;
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
  }
}
