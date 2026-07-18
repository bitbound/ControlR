using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Common.Services.Windows;

internal class DesktopSessionProviderWindows(
  IWin32Interop win32Interop,
  IIpcServerStore ipcStore,
  ILogger<DesktopSessionProviderWindows> logger) : IDesktopSessionProvider
{
  private readonly IIpcServerStore _ipcStore = ipcStore;
  private readonly ILogger<DesktopSessionProviderWindows> _logger = logger;
  private readonly IWin32Interop _win32Interop = win32Interop;

  public async Task<DesktopSession[]> GetActiveDesktopClients()
  {
    var serversBySession = new Dictionary<int, IpcServerRecord>();

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

      if (!serversBySession.TryGetValue(sessionId, out var existing) ||
          serverRecord.Server.IsConnected && !existing.Server.IsConnected ||
          serverRecord.Server.IsConnected == existing.Server.IsConnected && serverRecord.Process.Id > existing.Process.Id)
      {
        serversBySession[sessionId] = serverRecord;
      }
    }

    var uiSessions = new List<DesktopSession>(serversBySession.Count);

    foreach (var (sessionId, serverRecord) in serversBySession)
    {
      try
      {
        var sessionInfo = await serverRecord.Server.Client.GetDesktopSessionInfo();

        uiSessions.Add(new DesktopSession
        {
          AreRemoteControlPermissionsGranted = sessionInfo.AreRemoteControlPermissionsGranted,
          DesktopName = sessionInfo.DesktopName,
          Name = sessionInfo.Name,
          ProcessId = serverRecord.Process.Id,
          SystemSessionId = sessionInfo.SystemSessionId,
          Type = sessionInfo.SessionType,
          Username = sessionInfo.Username,
        });
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex,
          "Failed to get session info from DesktopClient process {ProcessId} in session {SessionId}.",
          serverRecord.Process.Id,
          sessionId);
      }
    }

    return [.. uiSessions];
  }

  public Task<string[]> GetLoggedInUsers()
  {
    var activeSessions = _win32Interop.GetActiveSessions();
    var loggedInUsers = activeSessions
      .Where(s => !string.IsNullOrEmpty(s.Username))
      .Select(s => s.Username)
      .Distinct()
      .ToArray();

    return loggedInUsers.AsTaskResult();
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
