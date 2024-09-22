using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Client.Services;

public interface IDeviceCache
{
  IEnumerable<DeviceDto> Devices { get; }
  Task AddOrUpdate(DeviceDto device);

  void Clear();

  Task Initialize();
  Task Remove(DeviceDto device);

  Task SetAllOffline();

  bool TryGet(Guid deviceId, [NotNullWhen(true)] out DeviceDto? device);
}

internal class DeviceCache(ILogger<DeviceCache> logger) : IDeviceCache
{
  private static readonly ConcurrentDictionary<Guid, DeviceDto> _cache = new();
  private static readonly SemaphoreSlim _initLock = new(1, 1);

  public IEnumerable<DeviceDto> Devices => _cache.Values;

  public Task AddOrUpdate(DeviceDto device)
  {
    _cache.AddOrUpdate(device.Id, device, (_, _) => device);
    return Task.CompletedTask;
  }

  public void Clear()
  {
    _cache.Clear();
  }

  public async Task Initialize()
  {
    await _initLock.WaitAsync();
    try
    {
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while trying to load device cache from file system.");
    }
    finally
    {
      _initLock.Release();
    }
  }

  public Task Remove(DeviceDto device)
  {
    _cache.Remove(device.Id, out _);
    return Task.CompletedTask;
  }

  public Task SetAllOffline()
  {
    foreach (var device in _cache.Values)
    {
      device.IsOnline = false;
    }

    return Task.CompletedTask;
  }

  public bool TryGet(Guid deviceId, [NotNullWhen(true)] out DeviceDto? device)
  {
    return _cache.TryGetValue(deviceId, out device);
  }
}