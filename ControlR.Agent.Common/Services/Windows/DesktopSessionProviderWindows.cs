using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.Processes;

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
    var serversByWinSession = new Dictionary<int, IpcServerRecord>();

    foreach (var serverRecord in _ipcStore.Servers.Values)
    {
      if (!TryGetProcessSessionId(serverRecord.Process, out var sessionId))
      {
        if (_ipcStore.TryRemove(serverRecord.Process.Id, out var removedRecord) && removedRecord is not null)
        {
          Disposer.DisposeAll(removedRecord.Process, removedRecord.Server);
        }

        continue;
      }

      if (!serversByWinSession.TryGetValue(sessionId, out var existingRecord) ||
          serverRecord.Server.IsConnected && !existingRecord.Server.IsConnected ||
          serverRecord.Server.IsConnected == existingRecord.Server.IsConnected && serverRecord.Process.Id > existingRecord.Process.Id)
      {
        serversByWinSession[sessionId] = serverRecord;
      }
    }

    var uiSessions = new List<DesktopSession>(serversByWinSession.Count);

    foreach (var server in serversByWinSession.Values)
    {
      if (!TryGetProcessSessionId(server.Process, out var sessionId))
      {
        if (_ipcStore.TryRemove(server.Process.Id, out var removedRecord) && removedRecord is not null)
        {
          Disposer.DisposeAll(removedRecord.Process, removedRecord.Server);
        }

        continue;
      }

      if (!windowsSessions.TryGetValue(sessionId, out var winSession))
      {
        continue;
      }

      uiSessions.Add(new DesktopSession
      {
        AreRemoteControlPermissionsGranted = true, // Windows doesn't require special permissions
        ProcessId = server.Process.Id,
        SystemSessionId = sessionId,
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

  private static bool TryGetProcessSessionId(IProcess process, out int sessionId)
  {
    try
    {
      sessionId = process.SessionId;
      return true;
    }
    catch
    {
      sessionId = -1;
      return false;
    }
  }
}
