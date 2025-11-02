using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;

namespace ControlR.Agent.Common.Services.Mac;
internal class DesktopSessionProviderMac(
  IIpcServerStore ipcStore,
  IProcessManager processManager,
  ILogger<DesktopSessionProviderMac> logger) : IDesktopSessionProvider
{
  private readonly IIpcServerStore _ipcStore = ipcStore;
  private readonly ILogger<DesktopSessionProviderMac> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  
  public async Task<DesktopSession[]> GetActiveDesktopClients()
  {
    var uiSessions = new List<DesktopSession>();
    var loggedInUsers = await GetLoggedInUserMap();

    foreach (var server in _ipcStore.Servers)
    {
      var uiSession = new DesktopSession()
      {
        ProcessId = server.Value.Process.Id,
        SystemSessionId = server.Value.Process.SessionId,
        Type = DesktopSessionType.Console // On macOS, we typically have console sessions
      };

      // Try to find the username for this session
      var getUserResult = await TryGetUsernameForProcess(server.Value.Process, loggedInUsers);
      if (getUserResult.IsSuccess)
      {
        uiSession.Username = getUserResult.Value;
        uiSession.Name = $"Console - {getUserResult.Value}";
      }
      else
      {
        uiSession.Name = $"Console - Session {server.Value.Process.SessionId}";
      }

      uiSessions.Add(uiSession);
    }

    return [.. uiSessions];
  }

  public async Task<string[]> GetLoggedInUsers()
  {
    var sessions = await GetActiveUiSessions();
    return sessions
      .Where(s => !string.IsNullOrEmpty(s.Username))
      .Select(s => s.Username)
      .Distinct()
      .ToArray();
  }


  private static DesktopSessionType DetermineSessionType(string tty)
  {
    // On macOS:
    // - "console" typically means the main physical display
    // - "ttys000", "ttys001", etc. are typically remote sessions or additional terminals
    // For simplicity, we'll treat everything as Console since macOS doesn't have
    // the same RDP concept as Windows
    return DesktopSessionType.Console;
  }

  private static string FormatSessionName(string username, string tty, DesktopSessionType sessionType)
  {
    var sessionTypeStr = sessionType == DesktopSessionType.Console ? "Console" : "Remote";
    
    if (tty == "console")
    {
      return $"{sessionTypeStr} - {username}";
    }
    else
    {
      return $"{sessionTypeStr} - {username} ({tty})";
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


  private async Task AddVncSessions(List<DesktopSession> sessions)
  {
    try
    {
      // Check for VNC/Screen Sharing processes
      var vncResult = await _processManager.GetProcessOutput("ps", "aux", 5000);
      if (!vncResult.IsSuccess)
        return;

      var vncLines = vncResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in vncLines)
      {
        // Look for VNC server processes or Screen Sharing
        if (line.Contains("vnc", StringComparison.OrdinalIgnoreCase) || 
            line.Contains("screensharing", StringComparison.OrdinalIgnoreCase))
        {
          var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
          if (parts.Length < 2) continue;

          var vncUsername = parts[0];
          
          // Avoid duplicates and system users
          if (sessions.Any(s => s.Username == vncUsername) || vncUsername == "root")
            continue;

          // Generate a deterministic session ID for VNC sessions
          var sessionId = GenerateSessionId(vncUsername, "vnc");

          var session = new DesktopSession
          {
            Username = vncUsername,
            SystemSessionId = sessionId,
            Name = $"Screen Sharing - {vncUsername}",
            Type = DesktopSessionType.Console, // Treat VNC as console since it's the desktop
            ProcessId = 0
          };

          sessions.Add(session);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Error detecting VNC sessions");
    }
  }

  private async Task<DesktopSession[]> GetActiveUiSessions()
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
              uid < 500) // Exclude system users
            continue;

          // Generate a deterministic session ID based on username and TTY
          var sessionId = GenerateSessionId(username, tty);

          // Determine session type based on tty
          var sessionType = DetermineSessionType(tty);
          var sessionName = FormatSessionName(username, tty, sessionType);

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

      // Also check for VNC/Screen Sharing sessions
      await AddVncSessions(sessions);
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
        // Format: username console timestamp
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1)
          continue;

        var username = parts[0];
        // Get UID for the user
        var uidResult = await _processManager.GetProcessOutput("id", $"-u {username}", 3000);
        if (!uidResult.IsSuccess ||
            string.IsNullOrWhiteSpace(uidResult.Value) ||
            !int.TryParse(uidResult.Value.Trim(), out var uid) ||
            uid < 500) // Exclude system users (typically UID < 500)
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
        var usernameResult = _processManager.GetProcessOutput("id", $"-un {uid}", 3000).GetAwaiter().GetResult();
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
