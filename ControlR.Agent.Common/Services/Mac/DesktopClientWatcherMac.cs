using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Logging;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Mac;

internal class DesktopClientWatcherMac(
  TimeProvider timeProvider,
  IServiceControl serviceControl,
  IDesktopClientRepairCoordinator desktopClientRepairCoordinator,
  IProcessManager processManager,
  IFileSystem fileSystem,
  IFileSystemPathProvider fileSystemPathProvider,
  IDesktopClientFileVerifier desktopClientFileVerifier,
  ISystemEnvironment systemEnvironment,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<DesktopClientWatcherMac> logger) : BackgroundService
{
  private readonly LogDeduplicationContext<DesktopClientWatcherMac> _dedupeLogger = logger.EnterDedupeScope();
  private readonly IDesktopClientFileVerifier _desktopClientFileVerifier = desktopClientFileVerifier;
  private readonly IDesktopClientRepairCoordinator _desktopClientRepairCoordinator = desktopClientRepairCoordinator;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IFileSystemPathProvider _fileSystemPathProvider = fileSystemPathProvider;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILogger<DesktopClientWatcherMac> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  private readonly IServiceControl _serviceControl = serviceControl;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;

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
        await CheckAndStartDesktopClientServices(stoppingToken);
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Desktop process watcher is stopping due to cancellation.");
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while checking for desktop processes.");
      }
    }
    _dedupeLogger.TryDispose();
    await _serviceControl.StopDesktopClientService(throwOnFailure: false);
  }

  private static string GetRepairKey(string uid)
  {
    return $"mac-user-{uid}";
  }

  private async Task CheckAndStartDesktopClientServices(CancellationToken cancellationToken)
  {
    try
    {
      // Get all logged-in users
      var loggedInUsers = await GetLoggedInUsersAsync();
      if (loggedInUsers.Count == 0)
      {
        _dedupeLogger.LogDeduped(LogLevel.Information, "No logged-in users found.");
        return;
      }

      _dedupeLogger.LogDeduped(LogLevel.Information, "Found {UserCount} logged-in users.", args: loggedInUsers.Count);

      foreach (var uid in loggedInUsers)
      {
        await CheckDesktopClientForUser(uid, cancellationToken);
      }
    }
    catch (Exception ex)
    {
      _dedupeLogger.LogDeduped(LogLevel.Error, "Error checking desktop client services.", exception: ex);
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
        _dedupeLogger.LogDeduped(LogLevel.Information, "Desktop client service not running for user {UID}. Starting service.", args: uid);
        await _serviceControl.StartDesktopClientService(throwOnFailure: true);
      }
      else
      {
        _desktopClientRepairCoordinator.ReportHealthy(repairKey);
        _dedupeLogger.LogDeduped(LogLevel.Information, "Desktop client service is running for user {UID}.", args: uid);
      }
    }
    catch (Exception ex)
    {
      _desktopClientRepairCoordinator.ReportFailure(
        GetRepairKey(uid),
        $"Failed to check or start the desktop client for user {uid}: {ex.Message}");
      _dedupeLogger.LogDeduped(LogLevel.Warning, "Failed to check/start desktop client service for user {UID}.", args: uid, exception: ex);
    }
  }

  private string GetDesktopClientPlistPath()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "/Library/LaunchAgents/app.controlr.desktop.plist";
    }

    return $"/Library/LaunchAgents/app.controlr.desktop.{_instanceOptions.Value.InstanceId}.plist";
  }

  private string GetDesktopClientServiceName()
  {
    // This should match the service name from the LaunchAgent plist
    // Based on ServiceControlMac implementation
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "app.controlr.desktop";
    }

    return $"app.controlr.desktop.{_instanceOptions.Value.InstanceId}";
  }

  private async Task<List<string>> GetLoggedInUsersAsync()
  {
    try
    {
      var result = await _processManager.GetProcessOutput("who", "-u", 5000);
      var users = new List<string>();

      if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
      {
        var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
          // Parse the who output to extract usernames
          // Format: username console timestamp
          var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
          if (parts.Length >= 1)
          {
            var username = parts[0];
            // Get UID for the user
            var uidResult = await _processManager.GetProcessOutput("id", $"-u {username}", 3000);
            if (uidResult.IsSuccess &&
                !string.IsNullOrWhiteSpace(uidResult.Value) &&
                int.TryParse(uidResult.Value.Trim(), out var uid) &&
                uid >= 500) // Exclude system users (typically UID < 500)
            {
              users.Add(uidResult.Value.Trim());
            }
          }
        }
      }

      return [.. users.Distinct()];
    }
    catch (Exception ex)
    {
      _dedupeLogger.LogDeduped(LogLevel.Error, "Failed to get logged-in users. Falling back to empty list.", exception: ex);
      return [];
    }
  }

  private async Task<bool> IsDesktopClientServiceRunning(string uid, string serviceName)
  {
    try
    {
      // Use launchctl to check if the service is running
      var result = await _processManager.GetProcessOutput("sudo", $"launchctl print gui/{uid}/{serviceName}", 5000);

      if (!result.IsSuccess)
      {
        _dedupeLogger.LogDeduped(LogLevel.Warning, "Service {ServiceName} not found for user {UID}: {Reason}", args: [serviceName, uid, result.Reason]);
        return false;
      }

      // If the command succeeds and contains process information, the service is running
      var output = result.Value;
      if (string.IsNullOrWhiteSpace(output))
      {
        return false;
      }

      // Check if the output contains "pid = " which indicates a running process
      var isRunning = output.Contains("pid = ");
      _dedupeLogger.LogDeduped(LogLevel.Information, "Service {ServiceName} status for user {UID}: {Status}", args: [serviceName, uid, isRunning ? "Running" : "Not running"]);

      return isRunning;
    }
    catch (Exception ex)
    {
      _dedupeLogger.LogDeduped(LogLevel.Warning, "Failed to check service status for user {UID}.", args: [uid], exception: ex);
      return false;
    }
  }

  private Result ValidateDesktopClientInstallation()
  {
    var desktopExecutablePath = _fileSystemPathProvider.GetDesktopExecutablePath();
    if (!_fileSystem.FileExists(desktopExecutablePath))
    {
      return Result.Fail($"Desktop client executable was not found at '{desktopExecutablePath}'.");
    }

    var desktopClientPlistPath = GetDesktopClientPlistPath();
    if (!_fileSystem.FileExists(desktopClientPlistPath))
    {
      return Result.Fail($"Desktop client LaunchAgent plist was not found at '{desktopClientPlistPath}'.");
    }

    var verificationResult = _desktopClientFileVerifier.VerifyFile(desktopExecutablePath);
    if (verificationResult.IsSuccess)
    {
      _desktopClientRepairCoordinator.ReportHealthy("desktop-installation");
      return verificationResult;
    }

    return verificationResult;
  }
}
