using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.NativeInterop.Windows;

namespace ControlR.Agent.Common.Services.Windows;

internal class UiSessionProviderWindows(
  IWin32Interop win32Interop,
  IIpcServerStore ipcStore) : IUiSessionProvider
{
  private readonly IWin32Interop _win32Interop = win32Interop;
  private readonly IIpcServerStore _ipcStore = ipcStore;

  public Task<DeviceUiSession[]> GetActiveDesktopClients()
  {
    var uiSessions = new List<DeviceUiSession>();
    var windowsSessions = _win32Interop
      .GetActiveSessions()
      .ToDictionary(x => x.SystemSessionId, x => x);

    foreach (var server in _ipcStore.Servers)
    {
      if (!windowsSessions.TryGetValue(server.Value.Process.SessionId, out var winSession))
      {
        continue;
      }
      var uiSession = new DeviceUiSession()
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
