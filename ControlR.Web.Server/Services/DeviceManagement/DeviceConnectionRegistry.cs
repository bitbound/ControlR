using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;

namespace ControlR.Web.Server.Services.DeviceManagement;

/// <summary>
/// Tracks the SignalR connection that currently belongs to each agent on this server. Set when a device
/// registers or updates, removed when it goes offline. Hub handlers read it first so forwarding to an
/// agent does not require a database read per message.
/// </summary>
public interface IDeviceConnectionRegistry
{
  void Remove(Guid deviceId, string connectionId);
  void Set(Guid deviceId, string connectionId);
  bool TryGetConnectionId(Guid deviceId, [NotNullWhen(true)] out string? connectionId);
}

public class DeviceConnectionRegistry : IDeviceConnectionRegistry
{
  private readonly ConcurrentDictionary<Guid, string> _connectionIds = [];

  public void Remove(Guid deviceId, string connectionId)
  {
    // The key-value overload keeps a stale disconnect from evicting the entry of an agent that has
    // already reconnected.
    _connectionIds.TryRemove(new KeyValuePair<Guid, string>(deviceId, connectionId));
  }

  public void Set(Guid deviceId, string connectionId)
  {
    _connectionIds[deviceId] = connectionId;
  }

  public bool TryGetConnectionId(Guid deviceId, [NotNullWhen(true)] out string? connectionId)
  {
    return _connectionIds.TryGetValue(deviceId, out connectionId);
  }
}
