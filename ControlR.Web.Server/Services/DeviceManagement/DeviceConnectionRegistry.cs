using System.Collections.Concurrent;

namespace ControlR.Web.Server.Services.DeviceManagement;

/// <summary>
/// Tracks the SignalR connection that currently belongs to each agent. Set when a device registers or
/// updates, removed when it goes offline. Hub handlers read it first so forwarding to an agent does not
/// require a database read per message.
/// </summary>
/// <remarks>
/// Members return tasks so a shared store can replace the in-process dictionary without changing
/// callers.
/// </remarks>
public interface IDeviceConnectionRegistry
{
  Task<string?> GetConnectionId(Guid deviceId, CancellationToken cancellationToken = default);
  Task Remove(Guid deviceId, string connectionId, CancellationToken cancellationToken = default);
  Task Set(Guid deviceId, string connectionId, CancellationToken cancellationToken = default);
}

public class DeviceConnectionRegistry : IDeviceConnectionRegistry
{
  private readonly ConcurrentDictionary<Guid, string> _connectionIds = [];

  public Task<string?> GetConnectionId(Guid deviceId, CancellationToken cancellationToken = default)
  {
    _connectionIds.TryGetValue(deviceId, out var connectionId);
    return Task.FromResult(connectionId);
  }

  public Task Remove(Guid deviceId, string connectionId, CancellationToken cancellationToken = default)
  {
    // The key-value overload keeps a stale disconnect from evicting the entry of an agent that has
    // already reconnected.
    _connectionIds.TryRemove(new KeyValuePair<Guid, string>(deviceId, connectionId));
    return Task.CompletedTask;
  }

  public Task Set(Guid deviceId, string connectionId, CancellationToken cancellationToken = default)
  {
    _connectionIds[deviceId] = connectionId;
    return Task.CompletedTask;
  }
}
