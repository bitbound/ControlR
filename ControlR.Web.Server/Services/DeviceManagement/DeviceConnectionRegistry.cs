using System.Collections.Concurrent;

namespace ControlR.Web.Server.Services.DeviceManagement;

/// <summary>
/// Tracks the SignalR connection that currently belongs to each agent. Set when a device registers or
/// updates, removed when it goes offline. Hub handlers read it first so forwarding to an agent does not
/// require a database read per message.
/// </summary>
/// <remarks>
/// Members return tasks so a shared store can replace the in-process dictionary without changing
/// callers. The read reports absence as a failure result, because a missing entry is an ordinary
/// outcome and the caller falls back to the device row. The two writes have no such outcome, so a
/// store fault there surfaces as an exception.
/// </remarks>
public interface IDeviceConnectionRegistry
{
  Task<Result<string>> GetConnectionId(Guid deviceId, CancellationToken cancellationToken = default);
  Task Remove(Guid deviceId, string connectionId, CancellationToken cancellationToken = default);
  Task Set(Guid deviceId, string connectionId, CancellationToken cancellationToken = default);
}

public class DeviceConnectionRegistry : IDeviceConnectionRegistry
{
  private readonly ConcurrentDictionary<Guid, string> _connectionIds = [];

  public Task<Result<string>> GetConnectionId(Guid deviceId, CancellationToken cancellationToken = default)
  {
    if (_connectionIds.TryGetValue(deviceId, out var connectionId))
    {
      return Task.FromResult(Result.Ok(connectionId));
    }

    return Task.FromResult(Result.Fail<string>("No agent connection is registered for this device."));
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
