using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Shared.Services.Linux;

/// <summary>
///   Provides methods to detect logged-in users on the current platform.
/// </summary>
public interface ILoggedInUserProvider
{
  /// <summary>
  ///   Gets a list of UIDs for users with an active graphical session (X11 or
  ///   Wayland). Excludes system users (UID <1000), display manager sessions,
  ///   and text/SSH sessions, which cannot host the desktop client.
  /// </summary>
  /// <returns>A list of UIDs as strings.</returns>
  Task<List<string>> GetLoggedInUserUids();
}

internal class LoggedInUserProviderLinux(
  IProcessManager processManager,
  ILogger<LoggedInUserProviderLinux> logger) : ILoggedInUserProvider
{
  private readonly ILogger<LoggedInUserProviderLinux> _logger = logger;
  private readonly IProcessManager _processManager = processManager;

  public async Task<List<string>> GetLoggedInUserUids()
  {
    try
    {
      var users = new List<string>();

      // Use loginctl to get active user sessions
      var sessionsResult = await _processManager.GetProcessOutput("loginctl", "list-sessions --no-legend", 3000);
      if (!sessionsResult.IsSuccess || string.IsNullOrWhiteSpace(sessionsResult.Value))
      {
        return users;
      }

      var sessionLines = sessionsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in sessionLines)
      {
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 0)
        {
          continue;
        }

        var sessionId = parts[0]; // The session ID is always the first column

        // Get the detailed session info using show-session (key-value format)
        var sessionInfoResult = await _processManager.GetProcessOutput("loginctl", $"show-session {sessionId}", 3000);
        if (sessionInfoResult.IsSuccess && !string.IsNullOrWhiteSpace(sessionInfoResult.Value))
        {
          var sessionInfo = ParseSessionInfo(sessionInfoResult.Value);

          // Skip sessions that are closing.
          if (sessionInfo.TryGetValue("State", out var sessionState) &&
              sessionState?.Equals("closing", StringComparison.OrdinalIgnoreCase) == true)
          {
            continue;
          }

          // Skip sessions that are display manager sessions.
          if (sessionInfo.TryGetValue("User", out var userValue) &&
              IsDisplayManagerUser(userValue))
          {
            continue;
          }

          // Only consider graphical sessions (x11 or wayland). The desktop client
          // is a GUI application and cannot run in a text/SSH session, so users
          // without a graphical session (e.g. an SSH-only user at the login screen)
          // must be excluded.
          if (!sessionInfo.TryGetValue("Type", out var sessionType) ||
              (!string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase)))
          {
            continue;
          }

          // Only a session that is actually active at the seat can host the desktop client.
          // This mirrors DesktopEnvironmentDetectorAgent; without it a graphical session that
          // is not active (greeter, switched VT, inactive seat) starts a client for a user
          // who is not at the console.
          if (!sessionInfo.TryGetValue("Active", out var activeValue) ||
              !string.Equals(activeValue, "yes", StringComparison.OrdinalIgnoreCase))
          {
            continue;
          }

          // Get UID from regular user sessions (UID >= 1000)
          if (sessionInfo.TryGetValue("User", out userValue) &&
              int.TryParse(userValue, out var uid) &&
              uid >= 1000)
          {
            users.Add(userValue);
          }
        }
      }

      return [.. users.Distinct()];
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to get logged-in users. Falling back to empty list.");
      return [];
    }
  }

  private static bool IsDisplayManagerUser(string userValue)
  {
    // List of common display manager usernames
    string[] displayManagerUsers = { "gdm", "lightdm", "sddm" };

    // Check if userValue is in the list of displayManagerUsers
    return displayManagerUsers.Any(user => string.Equals(userValue, user, StringComparison.OrdinalIgnoreCase));
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
}