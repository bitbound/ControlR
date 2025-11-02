using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ControlR.Agent.Common.Services.Linux;

internal class DesktopClientWatcherLinux(
  TimeProvider timeProvider,
  IServiceControl serviceControl,
  IProcessManager processManager,
  IHeadlessServerDetector headlessServerDetector,
  ISystemEnvironment systemEnvironment,
  IFileSystem fileSystem,
  IControlrMutationLock mutationLock,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<DesktopClientWatcherLinux> logger) : BackgroundService
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IHeadlessServerDetector _headlessServerDetector = headlessServerDetector;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILogger<DesktopClientWatcherLinux> _logger = logger;
  private readonly IControlrMutationLock _mutationLock = mutationLock;
  private readonly IProcessManager _processManager = processManager;
  private readonly IServiceControl _serviceControl = serviceControl;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;

  private int? _loginScreenDesktopClientPid;

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
          _logger.LogIfChanged(LogLevel.Information, "Running on headless Ubuntu server. Desktop client services are not applicable.");
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
  
  private static Dictionary<string, string> ParseSessionInfo(string sessionOutput)
  {
    var info = new Dictionary<string, string>();
    var lines = sessionOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var line in lines)
    {
      var parts = line.Split('=', 2);
      if (parts.Length == 2)
      {
        var key = parts[0].Trim();
        var value = parts[1].Trim();
        info[key] = value;
      }
    }
    
    return info;
  }

  private async Task CheckAndStartDesktopClientServices(CancellationToken cancellationToken)
  {
    try
    {
      // Check for login screen first
      await CheckLoginScreenDesktopClient(cancellationToken);

      // Then check logged-in users
      var loggedInUsers = await GetLoggedInUsersAsync();
      if (loggedInUsers.Count == 0)
      {
        _logger.LogIfChanged(LogLevel.Debug, "No logged-in users found.");
        return;
      }

      _logger.LogIfChanged(LogLevel.Debug, "Found {UserCount} logged-in users.", args: loggedInUsers.Count);

      foreach (var uid in loggedInUsers)
      {
        await CheckDesktopClientForUser(uid, cancellationToken);
      }
    }
    catch (Exception ex)
    {
      _logger.LogIfChanged(LogLevel.Error, "Error checking desktop client services.", exception: ex);
    }
  }

  private async Task CheckDesktopClientForUser(string uid, CancellationToken cancellationToken)
  {
    try
    {
      var serviceName = GetDesktopClientServiceName();
      var isRunning = await IsDesktopClientServiceRunning(uid, serviceName);

      if (!isRunning)
      {
        _logger.LogIfChanged(LogLevel.Information, "Desktop client service not running for user {UID}. Starting service.", args: uid);
        await _serviceControl.StartDesktopClientService(throwOnFailure: true);
      }
      else
      {
        _logger.LogIfChanged(LogLevel.Debug, "Desktop client service is running for user {UID}.", args: uid);
      }
    }
    catch (Exception ex)
    {
      _logger.LogIfChanged(LogLevel.Warning, "Failed to check/start desktop client service for user {UID}.", args: uid, exception: ex);
    }
  }

  private async Task CheckLoginScreenDesktopClient(CancellationToken cancellationToken)
  {
    var displayInfo = await DetectDisplayEnvironment();
    if (!displayInfo.IsLoginScreen)
    {
      // If not at login screen, ensure login screen client is stopped
      await StopLoginScreenDesktopClient();
      return;
    }

    _logger.LogIfChanged(
      LogLevel.Information,
      "Login screen detected. Display: {Display}, XAuth: {XAuth}, DM: {DisplayManager}",
      args: (displayInfo.Display, displayInfo.XAuthPath ?? "none", displayInfo.DisplayManager ?? "unknown"));

    // Launch desktop client for login screen if needed
    if (!await IsLoginScreenDesktopClientRunning())
    {
      await LaunchLoginScreenDesktopClient(displayInfo, cancellationToken);
    }
  }

  private Task<string> DetectCurrentDisplay()
  {
    try
    {
      // Check for active X11 displays
      var displays = new[] { ":0", ":1", ":10" }; // Common display numbers

      foreach (var display in displays)
      {
        if (_fileSystem.FileExists($"/tmp/.X11-unix/X{display[1..]}"))
        {
          return Task.FromResult(display);
        }
      }

      // Fallback to environment variable or default
      return Task.FromResult(Environment.GetEnvironmentVariable("DISPLAY") ?? ":0");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error detecting current display");
      return Task.FromResult(":0");
    }
  }

  private async Task<string?> DetectCurrentXAuthPath(string? displayManager)
  {
    try
    {
      // Method 1: Check display manager specific patterns
      switch (displayManager?.ToLowerInvariant())
      {
        case "sddm":
          // SDDM creates temporary auth files in /tmp with random names
          var sddmAuth = await _processManager.GetProcessOutput("bash", "-c \"find /tmp -name 'xauth_*' -type f -newer /proc/1 2>/dev/null | head -1\"", 3000);
          if (sddmAuth.IsSuccess && !string.IsNullOrEmpty(sddmAuth.Value.Trim()))
          {
            return sddmAuth.Value.Trim();
          }
          break;

        case "gdm":
        case "gdm3":
          // GDM stores auth in /run/gdm3 or similar
          var gdmAuth = await _processManager.GetProcessOutput("bash", "-c \"find /run/gdm* -name '*database*' -type f 2>/dev/null | head -1\"", 3000);
          if (gdmAuth.IsSuccess && !string.IsNullOrEmpty(gdmAuth.Value.Trim()))
          {
            return gdmAuth.Value.Trim();
          }
          break;

        case "lightdm":
          // LightDM typically uses fixed paths
          if (_fileSystem.FileExists("/run/lightdm/root/:0"))
          {
            return "/run/lightdm/root/:0";
          }
          break;
      }

      // Method 2: Try to extract from running X server process
      var xorgCmd = await _processManager.GetProcessOutput("ps", "aux", 5000);
      if (xorgCmd.IsSuccess)
      {
        var xorgLines = xorgCmd.Value.Split('\n')
          .Where(line => line.Contains("Xorg") || line.Contains("/usr/bin/X"))
          .ToArray();

        foreach (var line in xorgLines)
        {
          var authMatch = Regex.Match(line, @"-auth\s+(\S+)");
          if (authMatch.Success)
          {
            var authPath = authMatch.Groups[1].Value;
            if (_fileSystem.FileExists(authPath))
            {
              return authPath;
            }
          }
        }
      }

      // Method 3: Common fallback locations
      var fallbackPaths = new[]
      {
        "/var/lib/gdm3/.Xauthority",
        "/run/user/0/.Xauthority",
        "/root/.Xauthority"
      };

      foreach (var path in fallbackPaths)
      {
        if (_fileSystem.FileExists(path))
        {
          return path;
        }
      }

      return null;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error detecting XAUTH path");
      return null;
    }
  }

  private async Task<DisplayEnvironmentInfo> DetectDisplayEnvironment()
  {
    var info = new DisplayEnvironmentInfo();

    try
    {
      // First, detect which display manager is running
      var dmResult = await _processManager.GetProcessOutput("systemctl", "status display-manager --no-pager -l", 3000);
      if (dmResult.IsSuccess && !string.IsNullOrWhiteSpace(dmResult.Value))
      {
        var output = dmResult.Value.ToLowerInvariant();
        if (output.Contains("sddm.service"))
        {
          info.DisplayManager = "sddm";
        }
        else if (output.Contains("gdm") || output.Contains("gdm3"))
        {
          info.DisplayManager = "gdm";
        }
        else if (output.Contains("lightdm"))
        {
          info.DisplayManager = "lightdm";
        }
      }

      // Check if we're at login screen by looking for active user sessions
      // Use a more robust approach that doesn't rely on column order
      var hasActiveUserSessions = await HasActiveUserSessions();

      info.IsLoginScreen = !hasActiveUserSessions;

      if (info.IsLoginScreen)
      {
        // Dynamically detect XAUTH and display for current session
        info.XAuthPath = await DetectCurrentXAuthPath(info.DisplayManager);
        info.Display = await DetectCurrentDisplay();
      }

      return info;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error detecting display environment");
      return info;
    }
  }

  private string GetDesktopClientServiceName()
  {
    // This should match the service name from ServiceControlLinux
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
          // Format: username pts/0 timestamp (IP)
          var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
          if (parts.Length >= 1)
          {
            var username = parts[0];
            // Get UID for the user
            var uidResult = await _processManager.GetProcessOutput("id", $"-u {username}", 3000);
            if (uidResult.IsSuccess &&
                !string.IsNullOrWhiteSpace(uidResult.Value) &&
                int.TryParse(uidResult.Value.Trim(), out var uid) &&
                uid >= 1000) // Exclude system users (typically UID < 1000 on Linux)
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
      _logger.LogIfChanged(LogLevel.Error, "Failed to get logged-in users. Falling back to empty list.", args: ex);
      return [];
    }
  }

  private async Task<bool> HasActiveUserSessions()
  {
    try
    {
      // First get a list of session IDs
      var sessionsResult = await _processManager.GetProcessOutput("loginctl", "list-sessions --no-legend", 3000);
      if (!sessionsResult.IsSuccess || string.IsNullOrWhiteSpace(sessionsResult.Value))
      {
        return false;
      }

      var sessionLines = sessionsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in sessionLines)
      {
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
          var sessionId = parts[0]; // Session ID is always the first column
          
          // Get detailed session info using show-session (key-value format)
          var sessionInfoResult = await _processManager.GetProcessOutput("loginctl", $"show-session {sessionId}", 3000);
          if (sessionInfoResult.IsSuccess && !string.IsNullOrWhiteSpace(sessionInfoResult.Value))
          {
            var sessionInfo = ParseSessionInfo(sessionInfoResult.Value);
            
            // Check if this is an active session with a regular user UID (≥1000)
            if (sessionInfo.TryGetValue("Active", out string? activeValue) &&
                sessionInfo.TryGetValue("User", out string? userValue) &&
                string.Equals(activeValue, "yes", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(userValue, out var uid) &&
                uid >= 1000)
            {
              return true;
            }
          }
        }
      }
      
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking for active user sessions");
      return false;
    }
  }

  private async Task<bool> IsDesktopClientServiceRunning(string uid, string serviceName)
  {
    try
    {
      // Use systemctl to check if the user service is running
      // Set XDG_RUNTIME_DIR for the user context
      var result = await _processManager.GetProcessOutput("sudo", $"-u #{uid} XDG_RUNTIME_DIR=/run/user/{uid} systemctl --user is-active {serviceName}", 5000);

      if (!result.IsSuccess)
      {
        _logger.LogIfChanged(LogLevel.Warning, "Service {ServiceName} not found for user {UID}: {Reason}", args: (serviceName, uid, result.Reason));
        return false;
      }

      // systemctl is-active returns "active" if the service is running
      var output = result.Value?.Trim();
      var isRunning = string.Equals(output, "active", StringComparison.OrdinalIgnoreCase);

      _logger.LogIfChanged(LogLevel.Debug, "Service {ServiceName} status for user {UID}: {Status}", args: (serviceName, uid, isRunning ? "Running" : "Not running"));

      return isRunning;
    }
    catch (Exception ex)
    {
      _logger.LogIfChanged(LogLevel.Warning, "Failed to check service status for user {UID}.", args: uid, exception: ex);
      return false;
    }
  }

  private async Task<bool> IsLoginScreenDesktopClientRunning()
  {
    try
    {
      if (_loginScreenDesktopClientPid.HasValue)
      {
        // Check if the process is still running
        var processCheck = await _processManager.GetProcessOutput("ps", $"-p {_loginScreenDesktopClientPid.Value}", 3000);
        if (processCheck.IsSuccess && processCheck.Value.Contains(_loginScreenDesktopClientPid.Value.ToString()))
        {
          return true;
        }
        else
        {
          _loginScreenDesktopClientPid = null;
        }
      }

      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking login screen desktop client status");
      return false;
    }
  }

  private Task LaunchLoginScreenDesktopClient(DisplayEnvironmentInfo displayInfo, CancellationToken cancellationToken)
  {
    try
    {
      var installDir = GetInstallDirectory();
      var desktopClientPath = Path.Combine(installDir, "DesktopClient", "ControlR.DesktopClient");

      if (!_fileSystem.FileExists(desktopClientPath))
      {
        _logger.LogError("Desktop client executable not found at {Path}", desktopClientPath);
        return Task.CompletedTask;
      }

      var instanceArgs = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
        ? ""
        : $" --instance-id {_instanceOptions.Value.InstanceId}";

      // Set up environment variables for X11 access
      var envVars = new Dictionary<string, string>
      {
        ["DISPLAY"] = displayInfo.Display,
        ["XDG_SESSION_TYPE"] = "x11",
        ["DOTNET_ENVIRONMENT"] = "Production"
      };

      if (!string.IsNullOrEmpty(displayInfo.XAuthPath))
      {
        envVars["XAUTHORITY"] = displayInfo.XAuthPath;
      }

      _logger.LogInformation("Starting login screen desktop client. Display: {Display}, XAUTH: {XAuth}",
        displayInfo.Display, displayInfo.XAuthPath ?? "none");

      // Start the process with proper environment
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
      if (process != null)
      {
        _loginScreenDesktopClientPid = process.Id;
        _logger.LogInformation("Login screen desktop client started with PID {PID}", process.Id);

        // Don't wait for the process to exit - it should run continuously
        _ = Task.Run(async () =>
        {
          try
          {
            await process.WaitForExitAsync(cancellationToken);
            _logger.LogInformation("Login screen desktop client (PID {PID}) has exited", process.Id);
            _loginScreenDesktopClientPid = null;
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error waiting for login screen desktop client process");
          }
        }, cancellationToken);
      }
      else
      {
        _logger.LogError("Failed to start login screen desktop client process");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error launching login screen desktop client");
    }

    return Task.CompletedTask;
  }

  private async Task StopLoginScreenDesktopClient()
  {
    try
    {
      if (!_loginScreenDesktopClientPid.HasValue)
      {
        return;
      }

      _logger.LogInformation("Stopping login screen desktop client (PID {PID})", _loginScreenDesktopClientPid.Value);

      // Try graceful termination first
      await _processManager.GetProcessOutput("kill", $"-TERM {_loginScreenDesktopClientPid.Value}", 3000);

      // Wait a moment for graceful shutdown
      await Task.Delay(2000);

      // Check if still running and force kill if necessary
      var processCheck = await _processManager.GetProcessOutput("ps", $"-p {_loginScreenDesktopClientPid.Value}", 3000);
      if (processCheck.IsSuccess && processCheck.Value.Contains(_loginScreenDesktopClientPid.Value.ToString()))
      {
        await _processManager.GetProcessOutput("kill", $"-KILL {_loginScreenDesktopClientPid.Value}", 3000);
      }

      _loginScreenDesktopClientPid = null;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error stopping login screen desktop client");
    }
  }
  
  private class DisplayEnvironmentInfo
  {
    public string Display { get; set; } = ":0";
    public string? DisplayManager { get; set; }
    public bool IsLoginScreen { get; set; }
    public string? XAuthPath { get; set; }
  }
}
