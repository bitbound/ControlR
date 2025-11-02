using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Agent.Common.Services;

/// <summary>
/// Stores IPC server connections to UI sessions.
/// </summary>
internal interface IIpcServerStore
{
  ReadOnlyDictionary<int, IpcServerRecord> Servers { get; }

  void AddServer(IProcess process, IIpcServer server);
  bool ContainsServer(int processId);
  Task KillAllServers(string reason);
  bool TryGetServer(int processId, [NotNullWhen(true)]out IpcServerRecord? serverRecord);
  bool TryRemove(int processId, [NotNullWhen(true)] out IpcServerRecord? serverRecord);
}

internal class IpcServerStore(ILogger<IpcServerStore> logger) : IIpcServerStore
{
  private readonly ConcurrentDictionary<int, IpcServerRecord> _ipcServers = [];
  private readonly ILogger<IpcServerStore> _logger = logger;

  public ReadOnlyDictionary<int, IpcServerRecord> Servers => new(_ipcServers);

  public void AddServer(IProcess process, IIpcServer server)
  {
    // If the attested process is already gone, don't add a stale server record.
    if (process.HasExited)
    {
      Disposer.DisposeAll(process, server);
      return;
    }

    // Ensure we receive exit notifications to promptly clean up.
    process.EnableRaisingEvents = true;
    process.Exited += (s, e) =>
    {
      _ipcServers.TryRemove(process.Id, out _);
      Disposer.DisposeAll(process);
    };

    var serverRecord = new IpcServerRecord(process, server);
    _ipcServers.AddOrUpdate(
      process.Id,
      serverRecord,
      (key, existing) =>
      {
        Disposer.DisposeAll(existing.Process, existing.Server);
        return serverRecord;
      }
    );
  }

  public bool ContainsServer(int processId)
  {
    return _ipcServers.ContainsKey(processId);
  }

  public async Task KillAllServers(string reason)
  {
    foreach (var server in _ipcServers.Values)
    {
      try
      {
        var dto = new ShutdownCommandDto(reason);
        await server.Server.Send(dto);
      }
      catch (Exception ex)
      {
         _logger.LogWarning(
          ex,
          "Failed to send shutdown command to IPC server for process {ProcessId}.",
          server.Process.Id);
      }
      Disposer.DisposeAll(server.Process, server.Server);
    }
    _ipcServers.Clear();
  }

  public bool TryGetServer(int processId, [NotNullWhen(true)] out IpcServerRecord? serverRecord)
  {
    return _ipcServers.TryGetValue(processId, out serverRecord);
  }

  public bool TryRemove(int processId, [NotNullWhen(true)] out IpcServerRecord? serverRecord)
  {
    return _ipcServers.TryRemove(processId, out serverRecord);
  }
}
internal record IpcServerRecord(IProcess Process, IIpcServer Server);
