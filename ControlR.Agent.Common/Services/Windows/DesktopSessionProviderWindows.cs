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
    var uiSessions = new List<DesktopSession>();
    var windowsSessions = _win32Interop
      .GetActiveSessions()
      .ToDictionary(x => x.SystemSessionId, x => x);

    foreach (var server in _ipcStore.Servers)
    {
      if (!windowsSessions.TryGetValue(server.Value.Process.SessionId, out var winSession))
      {
        continue;
      }
      var uiSession = new DesktopSession()
      {
        ProcessId = server.Value.Process.Id,
        SystemSessionId = server.Value.Process.SessionId,
        Name = winSession.Name,
        Username = winSession.Username,
        Type = winSession.Type,
      };

      uiSessions.Add(uiSession);
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
