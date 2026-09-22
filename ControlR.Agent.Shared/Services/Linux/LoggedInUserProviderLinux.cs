using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Shared.Services.Linux;

/// <summary>
///   Provides the users logged in to graphical sessions on the current platform.
/// </summary>
public interface ILoggedInUserProvider
{
  /// <summary>
  ///   Gets one entry per user with a graphical session (X11 or Wayland). System users
  ///   (UID &lt;1000), display-manager sessions, and closing sessions are excluded. Each
  ///   entry reports whether the user is currently active at the seat.
  /// </summary>
  /// <returns>The logged-in user sessions.</returns>
  Task<IReadOnlyList<LoggedInUserSession>> GetLoggedInUsers();
}

internal class LoggedInUserProviderLinux(
  IProcessManager processManager,
  ILogger<LoggedInUserProviderLinux> logger) : ILoggedInUserProvider
{
  private readonly ILogger<LoggedInUserProviderLinux> _logger = logger;
  private readonly IProcessManager _processManager = processManager;

  public async Task<IReadOnlyList<LoggedInUserSession>> GetLoggedInUsers()
  {
    try
    {
      // Aggregate per UID so a user with several sessions yields one entry. IsActive is true
      // when any of that user's graphical sessions is the active one at its seat.
      var sessionsByUid = new Dictionary<string, (string SessionType, bool IsActive)>();

      // Use loginctl to get active user sessions
      var sessionsResult = await _processManager.GetProcessOutput("loginctl", "list-sessions --no-legend", 3000);
      if (!sessionsResult.IsSuccess || string.IsNullOrWhiteSpace(sessionsResult.Value))
      {
        return [];
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
        if (!sessionInfoResult.IsSuccess || string.IsNullOrWhiteSpace(sessionInfoResult.Value))
        {
          continue;
        }

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

        // Get UID from regular user sessions (UID >= 1000)
        if (!sessionInfo.TryGetValue("User", out userValue) ||
            !int.TryParse(userValue, out var uid) ||
            uid < 1000)
        {
          continue;
        }

        // Whether the session is the active one at its seat is a fact returned to callers.
        // Launching a client requires an active seat; tearing one down does not, so the two
        // policies cannot live in the provider. See #157.
        sessionInfo.TryGetValue("Active", out var activeValue);
        var isActive = string.Equals(activeValue, "yes", StringComparison.OrdinalIgnoreCase);

        if (sessionsByUid.TryGetValue(userValue, out var existing))
        {
          sessionsByUid[userValue] = (existing.SessionType, existing.IsActive || isActive);
        }
        else
        {
          sessionsByUid[userValue] = (sessionType, isActive);
        }
      }

      return [.. sessionsByUid.Select(kvp =>
        new LoggedInUserSession(kvp.Key, kvp.Value.SessionType, kvp.Value.IsActive))];
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
