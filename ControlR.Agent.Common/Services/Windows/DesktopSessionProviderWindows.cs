using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.NativeInterop.Windows;

namespace ControlR.Agent.Common.Services.Windows;

internal class DesktopSessionProviderWindows(
  IWin32Interop win32Interop,
  IIpcServerStore ipcStore) : IDesktopSessionProvider
{
  private readonly IIpcServerStore _ipcStore = ipcStore;
  private readonly IWin32Interop _win32Interop = win32Interop;

  public Task<DesktopSession[]> GetActiveDesktopClients()
  {
    // Build a map of live Windows sessions we care about (Console + active RDP)
    var windowsSessions = _win32Interop
      .GetActiveSessions()
      .ToDictionary(x => x.SystemSessionId, x => x);

    // If multiple IPC servers are registered for the same Windows session (e.g., stale entries
    // from a previous DesktopClient PID or multiple installs), prefer:
    // 1) Connected servers over disconnected
    // 2) Highest PID (assumed most recent)
    var serversByWinSession = _ipcStore.Servers
      .Values
      .GroupBy(s => s.Process.SessionId)
      .Select(g => g
        .OrderByDescending(s => s.Server.IsConnected)
        .ThenByDescending(s => s.Process.Id)
        .First())
      .ToArray();

    var uiSessions = new List<DesktopSession>(serversByWinSession.Length);

    foreach (var server in serversByWinSession)
    {
      if (!windowsSessions.TryGetValue(server.Process.SessionId, out var winSession))
      {
        continue;
      }

      uiSessions.Add(new DesktopSession
      {
        AreRemoteControlPermissionsGranted = true, // Windows doesn't require special permissions
        ProcessId = server.Process.Id,
        SystemSessionId = server.Process.SessionId,
        Name = winSession.Name,
        Username = winSession.Username,
        Type = winSession.Type,
      });
    }

    return uiSessions.ToArray().AsTaskResult();
  }

  public Task<string[]> GetLoggedInUsers()
  {
    var activeSessions = _win32Interop.GetActiveSessions();
    var loggedInUsers = activeSessions
      .Where(s => !string.IsNullOrEmpty(s.Username))
      .Select(s => s.Username)
      .Distinct()
      .ToArray();

    return Task.FromResult(loggedInUsers);
  }
}
