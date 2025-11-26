using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Dtos.IpcDtos;

namespace ControlR.Agent.Common.Services.Linux;

internal class DesktopSessionProviderLinux(
  IIpcServerStore ipcStore,
  IProcessManager processManager,
  ILogger<DesktopSessionProviderLinux> logger) : IDesktopSessionProvider
{
  private readonly IIpcServerStore _ipcStore = ipcStore;
  private readonly ILogger<DesktopSessionProviderLinux> _logger = logger;
  private readonly IProcessManager _processManager = processManager;


  public async Task<DesktopSession[]> GetActiveDesktopClients()
  {
    var desktopSessions = new List<DesktopSession>();
    var loggedInUsers = await GetLoggedInUserMap();

    foreach (var server in _ipcStore.Servers)
    {
      var process = server.Value.Process;

      // Default values
      string? username = null;
      string? display = null;
      string? waylandDisplay = null;
      string? xdgSessionType = null;
      string? tty = null;

      // Try to find the username for this session
      var getUserResult = await TryGetUsernameForProcess(process, loggedInUsers);
      if (getUserResult.IsSuccess)
      {
        username = getUserResult.Value;
      }

      // Try to get the controlling TTY for the process
      try
      {
        var ttyResult = await _processManager.GetProcessOutput("ps", $"-o tty= -p {process.Id}", 3000);
        if (ttyResult.IsSuccess)
        {
          var raw = ttyResult.Value.Trim();
          // ps may return '?' when there's no tty
          if (!string.IsNullOrWhiteSpace(raw) && raw != "?")
          {
            tty = raw;
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogDebug(ex, "Failed to get TTY for process {Pid}", process.Id);
      }

      // Try to read DISPLAY / Wayland variables from the process environment
      try
      {
        var envResult = await _processManager.GetProcessOutput("cat", $"/proc/{process.Id}/environ", 3000);
        if (envResult.IsSuccess && !string.IsNullOrEmpty(envResult.Value))
        {
          // environ is NUL (\0) separated
          var entries = envResult.Value.Split('\0', StringSplitOptions.RemoveEmptyEntries);
          foreach (var entry in entries)
          {
            if (display is null && entry.StartsWith("DISPLAY=", StringComparison.Ordinal))
            {
              var val = entry[8..].Trim();
              if (!string.IsNullOrWhiteSpace(val))
                display = val;
            }
            else if (waylandDisplay is null && entry.StartsWith("WAYLAND_DISPLAY=", StringComparison.Ordinal))
            {
              var val = entry[16..].Trim();
              if (!string.IsNullOrWhiteSpace(val))
                waylandDisplay = val;
            }
            else if (xdgSessionType is null && entry.StartsWith("XDG_SESSION_TYPE=", StringComparison.Ordinal))
            {
              var val = entry[17..].Trim();
              if (!string.IsNullOrWhiteSpace(val))
                xdgSessionType = val.ToLowerInvariant();
            }
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogDebug(ex, "Failed to read environment for process {Pid}", process.Id);
      }

      // Prefer Wayland if detected (WAYLAND_DISPLAY or XDG_SESSION_TYPE=wayland), else DISPLAY (X11), else TTY
      string? sessionKey;
      string sessionLabel;
      DesktopSessionType sessionType;

      var isWayland = !string.IsNullOrWhiteSpace(waylandDisplay) || string.Equals(xdgSessionType, "wayland", StringComparison.Ordinal);

      if (isWayland)
      {
        sessionKey = waylandDisplay ?? "wayland";
        sessionLabel = sessionKey; // e.g. wayland-0
        sessionType = DesktopSessionType.Console;
      }
      else if (!string.IsNullOrWhiteSpace(display))
      {
        sessionKey = display;
        sessionLabel = display; // ":0" etc. FormatSessionName will render as X11
        sessionType = DesktopSessionType.Console;
      }
      else if (!string.IsNullOrWhiteSpace(waylandDisplay))
      {
        sessionKey = waylandDisplay;
        sessionLabel = waylandDisplay; // e.g. "wayland-0"
        sessionType = DesktopSessionType.Console;
      }
      else if (!string.IsNullOrWhiteSpace(tty))
      {
        sessionKey = tty;
        sessionLabel = tty;
        sessionType = DetermineSessionType(tty);
      }
      else
      {
        // Last resort: use PID-derived key to avoid collisions
        sessionKey = $"pid:{process.Id}";
        sessionLabel = $"Session {process.SessionId}";
        sessionType = DesktopSessionType.Console;
      }

      // Build a stable numeric SystemSessionId compatible with discovery logic
      var sessionIdSeedUser = string.IsNullOrWhiteSpace(username) ? "unknown" : username;
      var systemSessionId = GenerateSessionId(sessionIdSeedUser, sessionKey);

      // Compose name consistently with other discovery methods
      var name = FormatSessionName(sessionLabel, sessionType);

      var uiSession = new DesktopSession
      {
        ProcessId = process.Id,
        SystemSessionId = systemSessionId,
        Type = sessionType,
        Username = username ?? string.Empty,
        Name = name
      };

      // Check permissions - X11 doesn't need permissions, Wayland does
      if (isWayland)
      {
        uiSession.AreRemoteControlPermissionsGranted = await CheckDesktopRemoteControlPermissions(server.Value);
      }
      else
      {
        uiSession.AreRemoteControlPermissionsGranted = true;
      }

      desktopSessions.Add(uiSession);
    }

    return [.. desktopSessions];
  }

  public async Task<string[]> GetLoggedInUsers()
  {
    var sessions = await GetActiveDesktopSessions();
    return [.. sessions
      .Where(s => !string.IsNullOrEmpty(s.Username))
      .Select(s => s.Username)
      .Distinct()];
  }


  private static DesktopSessionType DetermineSessionType(string tty)
  {
    // On Linux:
    // - "tty1", "tty2", etc. are virtual consoles (text mode)
    // - ":0", ":1", etc. are X11 display sessions
    // - "pts/0", "pts/1", etc. are pseudo-terminals (SSH, etc.)
    // For X11 desktop sessions, we'll treat them as Console
    if (tty.StartsWith(':') || tty.StartsWith("tty"))
    {
      return DesktopSessionType.Console;
    }

    // For SSH and other remote sessions, we don't have RDP but treat as console
    return DesktopSessionType.Console;
  }

  private static string FormatSessionName(string tty, DesktopSessionType sessionType)
  {
    var sessionTypeStr = sessionType == DesktopSessionType.Console ? "Console" : "Remote";

    if (tty.StartsWith(':'))
    {
      return $"{sessionTypeStr} (X11: {tty})";
    }
    else if (tty.StartsWith("wayland", StringComparison.OrdinalIgnoreCase))
    {
      return $"{sessionTypeStr} (Wayland: {tty})";
    }
    else if (tty.StartsWith("tty"))
    {
      return $"{sessionTypeStr} ({tty})";
    }
    else
    {
      return $"{sessionTypeStr} ({tty})";
    }
  }

  /// <summary>
  /// Generate a deterministic session ID based on username and TTY.
  /// This should match the logic used by the desktop client.
  /// </summary>
  private static int GenerateSessionId(string username, string tty)
  {
    var sessionKey = $"{username}:{tty}";
    var hash = sessionKey.GetHashCode();

    // Make sure it's positive and in a reasonable range
    return Math.Abs(hash % 9000) + 1000; // Range: 1000-9999
  }



  private async Task AddX11Sessions(List<DesktopSession> sessions)
  {
    try
    {
      // Check for X11 sessions by looking for X server processes
      var xResult = await _processManager.GetProcessOutput("ps", "aux", 5000);
      if (!xResult.IsSuccess)
        return;

      var xLines = xResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in xLines)
      {
        // Look for X server processes (Xorg, X11, etc.)
        if ((line.Contains("/usr/bin/X", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("Xorg", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("/usr/lib/xorg/Xorg", StringComparison.OrdinalIgnoreCase)) &&
            !line.Contains("grep", StringComparison.OrdinalIgnoreCase))
        {
          var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
          if (parts.Length < 2) continue;

          var xUsername = parts[0];

          // Avoid duplicates and system users
          if (sessions.Any(s => s.Username == xUsername) || xUsername == "root")
            continue;

          // Extract display number from command line if possible
          var displayMatch = System.Text.RegularExpressions.Regex.Match(line, @":(\d+)");
          var display = displayMatch.Success ? $":{displayMatch.Groups[1].Value}" : ":0";

          // Generate a deterministic session ID for X11 sessions
          var sessionId = GenerateSessionId(xUsername, display);

          var session = new DesktopSession
          {
            Username = xUsername,
            SystemSessionId = sessionId,
            Name = $"Console - {xUsername} (X11 {display})",
            Type = DesktopSessionType.Console,
            ProcessId = 0
          };

          sessions.Add(session);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Error detecting X11 sessions");
    }
  }

  private async Task<bool> CheckDesktopRemoteControlPermissions(IpcServerRecord serverInfo)
  {
    try
    {
      var ipcDto = new CheckOsPermissionsIpcDto(serverInfo.Process.Id);
      var ipcResult = await serverInfo.Server.Invoke<CheckOsPermissionsIpcDto, CheckOsPermissionsResponseIpcDto>(ipcDto, timeoutMs: 3000);

      if (ipcResult.IsSuccess && ipcResult.Value is not null)
      {
        return ipcResult.Value.ArePermissionsGranted;
      }

      _logger.LogWarning("Failed to get permissions via IPC for process {ProcessId}", serverInfo.Process.Id);
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking permissions via IPC for process {ProcessId}", serverInfo.Process.Id);
      return false;
    }
  }

  private async Task<DesktopSession[]> GetActiveDesktopSessions()
  {
    var sessions = new List<DesktopSession>();

    try
    {
      var result = await _processManager.GetProcessOutput("who", "-u", 5000);

      if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Value))
      {
        return [.. sessions];
      }

      var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in lines)
      {
        try
        {
          // Parse the who output to extract session information
          // Format: username tty timestamp (pid) comment
          var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
          if (parts.Length < 2)
            continue;

          var username = parts[0];
          var tty = parts[1];

          // Get UID for the user to exclude system users
          var uidResult = await _processManager.GetProcessOutput("id", $"-u {username}", 3000);
          if (!uidResult.IsSuccess ||
              !int.TryParse(uidResult.Value.Trim(), out var uid) ||
              uid < 1000) // Exclude system users (typically UID < 1000 on Ubuntu)
            continue;

          // Generate a deterministic session ID based on username and TTY
          var sessionId = GenerateSessionId(username, tty);

          // Determine session type based on tty
          var sessionType = DetermineSessionType(tty);
          var sessionName = FormatSessionName(tty, sessionType);

          var session = new DesktopSession
          {
            Username = username,
            SystemSessionId = sessionId,
            Name = sessionName,
            Type = sessionType,
            ProcessId = 0 // Will be set when desktop client connects
          };

          sessions.Add(session);
        }
        catch (Exception ex)
        {
          _logger.LogDebug(ex, "Error parsing who output line: {Line}", line);
        }
      }

      // Also check for X11 sessions specifically
      await AddX11Sessions(sessions);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to get active UI sessions.");
    }

    return [.. sessions];
  }

  private async Task<Dictionary<string, string>> GetLoggedInUserMap()
  {
    var userMap = new Dictionary<string, string>(); // uid -> username

    try
    {
      var result = await _processManager.GetProcessOutput("who", "-u", 5000);
      if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Value))
        return userMap;

      var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in lines)
      {
        // Parse the who output to extract usernames
        // Format: username tty timestamp (pid) comment
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1)
          continue;

        var username = parts[0];
        // Get UID for the user
        var uidResult = await _processManager.GetProcessOutput("id", $"-u {username}", 3000);
        if (!uidResult.IsSuccess ||
            string.IsNullOrWhiteSpace(uidResult.Value) ||
            !int.TryParse(uidResult.Value.Trim(), out var uid) ||
            uid < 1000) // Exclude system users (typically UID < 1000 on Ubuntu)
          continue;

        userMap[uidResult.Value.Trim()] = username;
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to get logged-in users.");
    }

    return userMap;
  }

  private async Task<Result<string>> TryGetUsernameForProcess(IProcess process, Dictionary<string, string> loggedInUsers)
  {
    try
    {
      // Try to get the user ID of the process owner
      var result = await _processManager.GetProcessOutput("ps", $"-o uid= -p {process.Id}", 3000);
      if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
      {
        var uid = result.Value.Trim();
        if (loggedInUsers.TryGetValue(uid, out var foundUsername))
        {
          return Result.Ok(foundUsername);
        }

        // If not in logged-in users, try to get username from UID
        var usernameResult = await _processManager.GetProcessOutput("id", $"-un {uid}", 3000);
        if (usernameResult.IsSuccess && !string.IsNullOrWhiteSpace(usernameResult.Value))
        {
          return Result.Ok(usernameResult.Value.Trim());
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Failed to get username for process {ProcessId}", process.Id);
    }

    return Result.Fail<string>("Failed to get username");
  }
}
