using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
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
  void KillAllServers();
  bool TryGetServer(int processId, [NotNullWhen(true)]out IpcServerRecord? serverRecord);
  bool TryRemove(int processId, [NotNullWhen(true)] out IpcServerRecord? serverRecord);
}

internal class IpcServerStore : IIpcServerStore
{
  private readonly ConcurrentDictionary<int, IpcServerRecord> _ipcServers = [];

  public ReadOnlyDictionary<int, IpcServerRecord> Servers => new(_ipcServers);

  public void AddServer(IProcess process, IIpcServer server)
  {
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

  public void KillAllServers()
  {
    foreach (var server in _ipcServers.Values)
    {
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
