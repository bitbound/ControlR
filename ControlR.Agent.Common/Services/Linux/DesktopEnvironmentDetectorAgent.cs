using ControlR.Libraries.Shared.Logging;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Common.Services.Linux;

internal interface IDesktopEnvironmentDetectorAgent
{
  Task<DisplayEnvironmentInfo> DetectDisplayEnvironment();
}

internal class DesktopEnvironmentDetectorAgent(
  IProcessManager processManager,
  IFileSystem fileSystem,
  ILogger<DesktopEnvironmentDetectorAgent> logger) : IDesktopEnvironmentDetectorAgent
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<DesktopEnvironmentDetectorAgent> _logger = logger;
  private readonly IProcessManager _processManager = processManager;


  public async Task<DisplayEnvironmentInfo> DetectDisplayEnvironment()
  {
    var info = new DisplayEnvironmentInfo();

    try
    {
      if (await HasActiveUserSessions())
      {
        info.IsLoginScreen = false;
        return info;
      }

      if (await GetGreeterSession() is not { } greeterSession)
      {
        // A display server that no logind session owns (a container's virtual X server,
        // for example) is not a login screen. See #154.
        info.IsLoginScreen = false;
        return info;
      }

      info.IsLoginScreen = true;

      // Try to detect Wayland display first
      if (greeterSession.SessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase))
      {
        info.WaylandDisplay = "wayland-0";
        info.WaylandRuntimeDir = GetWaylandRuntimeDir(greeterSession);
        info.IsWayland = true;

        _logger.LogInformationDeduped("Detected Wayland session at login screen");
        return info;
      }

      // Fall back to X11 detection
      info.DisplayManager = await DetectDisplayManager();
      info.XAuthPath = await DetectCurrentXAuthPath(info.DisplayManager);
      info.Display = await DetectCurrentDisplay();

      return info;
    }
    catch (Exception ex)
    {
      _logger.LogErrorDeduped("Error detecting display environment", exception: ex);
      return info;
    }
  }

  private static bool IsDisplayManagerUser(string userValue)
  {
    string[] displayManagerUsers = ["gdm", "lightdm", "sddm"];
    return displayManagerUsers.Any(user => string.Equals(userValue, user, StringComparison.OrdinalIgnoreCase));
  }

  private static Dictionary<string, string> ParseSessionInfo(string sessionOutput)
  {
    var info = new Dictionary<string, string>();
    var lines = sessionOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    foreach (var line in lines)
    {
      var parts = line.Split('=', 2);
      if (parts.Length != 2)
      {
        continue;
      }

      var key = parts[0].Trim();
      var value = parts[1].Trim();
      info[key] = value;
    }

    return info;
  }

  private Task<string> DetectCurrentDisplay()
  {
    try
    {
      // Check for the active X11 displays
      var displays = new[] { ":0", ":1", ":10" };

      foreach (var display in displays)
      {
        if (_fileSystem.FileExists($"/tmp/.X11-unix/X{display[1..]}"))
        {
          return Task.FromResult(display);
        }
      }

      // Fall back to the environment variable or a default
      return Task.FromResult(Environment.GetEnvironmentVariable("DISPLAY") ?? ":0");
    }
    catch (Exception ex)
    {
      _logger.LogErrorDeduped("Error detecting current display", exception: ex);
      return Task.FromResult(":0");
    }
  }

  private async Task<string?> DetectCurrentXAuthPath(string? displayManager)
  {
    try
    {
      // Method 1: Check the display-manager-specific patterns
      switch (displayManager?.ToLowerInvariant())
      {
        case "sddm":
        {
          var sddmAuth = await _processManager.GetProcessOutput("bash", "-c \"find /tmp -name 'xauth_*' -type f -newer /proc/1 2>/dev/null | head -1\"", 3000);
          if (sddmAuth.IsSuccess && !string.IsNullOrEmpty(sddmAuth.Value.Trim()))
          {
            return sddmAuth.Value.Trim();
          }
          break;
        }

        case "gdm":
        case "gdm3":
        {
          var gdmAuth = await _processManager.GetProcessOutput("bash", "-c \"find /run/gdm* -name '*database*' -type f 2>/dev/null | head -1\"", 3000);
          if (gdmAuth.IsSuccess && !string.IsNullOrEmpty(gdmAuth.Value.Trim()))
          {
            return gdmAuth.Value.Trim();
          }
          break;
        }

        case "lightdm":
        {
          if (_fileSystem.FileExists("/run/lightdm/root/:0"))
          {
            return "/run/lightdm/root/:0";
          }
          break;
        }
      }

      // Method 2: Try to extract the auth path from the running X server process
      var xorgCmd = await _processManager.GetProcessOutput("ps", "aux", 5000);
      if (xorgCmd.IsSuccess)
      {
        var xorgLines = xorgCmd.Value.Split('\n')
          .Where(line => line.Contains("Xorg") || line.Contains("/usr/bin/X"))
          .ToArray();

        foreach (var line in xorgLines)
        {
          var authMatch = System.Text.RegularExpressions.Regex.Match(line, @"-auth\s+(\S+)");
          if (!authMatch.Success)
          {
            continue;
          }

          var authPath = authMatch.Groups[1].Value;
          if (_fileSystem.FileExists(authPath))
          {
            return authPath;
          }
        }
      }

      // Method 3: Check common fallback locations
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
      _logger.LogErrorDeduped("Error detecting XAUTH path", exception: ex);
      return null;
    }
  }

  private async Task<string?> DetectDisplayManager()
  {
    var dmResult = await _processManager.GetProcessOutput("systemctl", "status display-manager --no-pager -l", 3000);
    if (!dmResult.IsSuccess || string.IsNullOrWhiteSpace(dmResult.Value))
    {
      return null;
    }

    var output = dmResult.Value.ToLowerInvariant();
    if (output.Contains("sddm.service"))
    {
      return "sddm";
    }

    if (output.Contains("gdm") || output.Contains("gdm3"))
    {
      return "gdm";
    }

    if (output.Contains("lightdm"))
    {
      return "lightdm";
    }

    return null;
  }

  private async Task<GreeterSession?> GetGreeterSession()
  {
    var sessionsResult = await _processManager.GetProcessOutput("loginctl", "list-sessions --no-legend", 3000);
    if (!sessionsResult.IsSuccess || string.IsNullOrWhiteSpace(sessionsResult.Value))
    {
      return null;
    }

    var sessionLines = sessionsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    foreach (var line in sessionLines)
    {
      var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length == 0)
      {
        continue;
      }

      var sessionId = parts[0];

      // Get detailed session info
      var sessionInfoResult = await _processManager.GetProcessOutput("loginctl", $"show-session {sessionId}", 3000);
      if (!sessionInfoResult.IsSuccess || string.IsNullOrWhiteSpace(sessionInfoResult.Value))
      {
        continue;
      }

      var sessionInfo = ParseSessionInfo(sessionInfoResult.Value);

      if (!sessionInfo.TryGetValue("Class", out var sessionClass) ||
          !sessionClass.Equals("greeter", StringComparison.OrdinalIgnoreCase) ||
          !sessionInfo.TryGetValue("Type", out var sessionType) ||
          (!sessionType.Equals("x11", StringComparison.OrdinalIgnoreCase) &&
           !sessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase)) ||
          !sessionInfo.TryGetValue("User", out var uidString) ||
          !int.TryParse(uidString, out var uid))
      {
        continue;
      }

      return new GreeterSession(sessionId, sessionType, uid);
    }

    return null;
  }

  private string GetWaylandRuntimeDir(GreeterSession greeterSession)
  {
    var runtimeDir = $"/run/user/{greeterSession.Uid}";
    if (_fileSystem.FileExists(Path.Combine(runtimeDir, "wayland-0")))
    {
      return runtimeDir;
    }

    // Fallback: Check common root-owned Wayland socket locations
    string[] rootWaylandPaths = ["/run/user/0/wayland-0", "/run/wayland-0"];
    foreach (var socketPath in rootWaylandPaths)
    {
      if (!_fileSystem.FileExists(socketPath))
      {
        continue;
      }

      runtimeDir = Path.GetDirectoryName(socketPath) ?? runtimeDir;
      _logger.LogInformationDeduped("Found root Wayland socket at {SocketPath}, RuntimeDir={RuntimeDir}",
        args: [socketPath, runtimeDir]);
      return runtimeDir;
    }

    _logger.LogWarningDeduped(
      "Wayland greeter session {SessionId} has no wayland-0 socket. RuntimeDir={RuntimeDir}",
      args: [greeterSession.SessionId, runtimeDir]);

    return runtimeDir;
  }

  private async Task<bool> HasActiveUserSessions()
  {
    try
    {
      var sessionsResult = await _processManager.GetProcessOutput("loginctl", "list-sessions --no-legend", 3000);
      if (!sessionsResult.IsSuccess || string.IsNullOrWhiteSpace(sessionsResult.Value))
      {
        return false;
      }

      var sessionLines = sessionsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in sessionLines)
      {
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
          continue;
        }

        var sessionId = parts[0];

        // Get the detailed session info using show-session (key-value format)
        var sessionInfoResult = await _processManager.GetProcessOutput("loginctl", $"show-session {sessionId}", 3000);
        if (!sessionInfoResult.IsSuccess || string.IsNullOrWhiteSpace(sessionInfoResult.Value))
        {
          continue;
        }

        var sessionInfo = ParseSessionInfo(sessionInfoResult.Value);

        // Session is closing OR it's an active display manager session (login screen)
        if (sessionInfo.TryGetValue("State", out var sessionState) &&
            sessionInfo.TryGetValue("User", out var userValue) &&
            (sessionState?.Equals("closing", StringComparison.OrdinalIgnoreCase) == true ||
             (sessionState?.Equals("active", StringComparison.OrdinalIgnoreCase) == true &&
              IsDisplayManagerUser(userValue))))
        {
          continue;
        }

        // Check if this is an active session with a regular user UID (≥1000)
        if (sessionInfo.TryGetValue("Active", out var activeValue) &&
            sessionInfo.TryGetValue("User", out userValue) &&
            string.Equals(activeValue, "yes", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(userValue, out var uid) &&
            uid >= 1000)
        {
          return true;
        }
      }

      return false;
    }
    catch (Exception ex)
    {
      _logger.LogErrorDeduped("Error checking for active user sessions", exception: ex);
      return false;
    }
  }

  private readonly record struct GreeterSession(string SessionId, string SessionType, int Uid);
}

internal class DisplayEnvironmentInfo
{
  public string Display { get; set; } = ":0";
  public string? DisplayManager { get; set; }
  public bool IsLoginScreen { get; set; }
  public bool IsWayland { get; set; }
  public string? WaylandDisplay { get; set; }
  public string? WaylandRuntimeDir { get; set; }
  public string? XAuthPath { get; set; }
}
