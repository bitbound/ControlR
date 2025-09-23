using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;

namespace ControlR.Agent.Common.Services.Linux;
internal class UiSessionProviderX11(
  IIpcServerStore ipcStore,
  IProcessManager processManager,
  ILogger<UiSessionProviderX11> logger) : IUiSessionProvider
{
  private readonly IIpcServerStore _ipcStore = ipcStore;
  private readonly IProcessManager _processManager = processManager;
  private readonly ILogger<UiSessionProviderX11> _logger = logger;

  public async Task<DeviceUiSession[]> GetActiveDesktopClients()
  {
    var uiSessions = new List<DeviceUiSession>();
    var loggedInUsers = await GetLoggedInUserMap();

    foreach (var server in _ipcStore.Servers)
    {
      var uiSession = new DeviceUiSession()
      {
        ProcessId = server.Value.Process.Id,
        SystemSessionId = server.Value.Process.SessionId,
        Type = UiSessionType.Console // On Linux X11, we typically have console sessions
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

  public async Task<DeviceUiSession[]> GetActiveUiSessions()
  {
    var sessions = new List<DeviceUiSession>();

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
          var sessionName = FormatSessionName(username, tty, sessionType);

          var session = new DeviceUiSession
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

  private static UiSessionType DetermineSessionType(string tty)
  {
    // On Linux:
    // - "tty1", "tty2", etc. are virtual consoles (text mode)
    // - ":0", ":1", etc. are X11 display sessions
    // - "pts/0", "pts/1", etc. are pseudo-terminals (SSH, etc.)
    // For X11 desktop sessions, we'll treat them as Console
    if (tty.StartsWith(':') || tty.StartsWith("tty"))
    {
      return UiSessionType.Console;
    }
    
    // For SSH and other remote sessions, we don't have RDP but treat as console
    return UiSessionType.Console;
  }

  private static string FormatSessionName(string username, string tty, UiSessionType sessionType)
  {
    var sessionTypeStr = sessionType == UiSessionType.Console ? "Console" : "Remote";
    
    if (tty.StartsWith(':'))
    {
      return $"{sessionTypeStr} - {username} (X11 {tty})";
    }
    else if (tty.StartsWith("tty"))
    {
      return $"{sessionTypeStr} - {username} ({tty})";
    }
    else
    {
      return $"{sessionTypeStr} - {username} ({tty})";
    }
  }

  private async Task AddX11Sessions(List<DeviceUiSession> sessions)
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

          var session = new DeviceUiSession
          {
            Username = xUsername,
            SystemSessionId = sessionId,
            Name = $"Console - {xUsername} (X11 {display})",
            Type = UiSessionType.Console,
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
}
