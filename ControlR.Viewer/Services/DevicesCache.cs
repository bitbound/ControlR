using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using IFileIo = ControlR.Libraries.DevicesCommon.Services.IFileSystem;
using IFileSystem = Microsoft.Maui.Storage.IFileSystem;

namespace ControlR.Viewer.Services;

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

internal class DeviceCache(IFileSystem fileSystem, IFileIo fileIo, ILogger<DeviceCache> logger)
  : IDeviceCache
{
  private static readonly ConcurrentDictionary<Guid, DeviceDto> _cache = new();
  private static readonly SemaphoreSlim _fileLock = new(1, 1);
  private readonly string _deviceCachePath = Path.Combine(fileSystem.AppDataDirectory, "DeviceCache.json");

  public IEnumerable<DeviceDto> Devices => _cache.Values;

  public async Task AddOrUpdate(DeviceDto device)
  {
    _cache.AddOrUpdate(device.Id, device, (_, _) => device);
    await TrySaveCache();
  }

  public void Clear()
  {
    _cache.Clear();
  }

  public async Task Initialize()
  {
    await _fileLock.WaitAsync();
    try
    {
      _cache.Clear();

      if (!fileIo.FileExists(_deviceCachePath))
      {
        fileIo.CreateFile(_deviceCachePath).Close();
      }

      var content = fileIo.ReadAllText(_deviceCachePath);

      if (string.IsNullOrWhiteSpace(content))
      {
        return;
      }

      var devices = JsonSerializer.Deserialize<DeviceDto[]>(content);

      if (devices is null)
      {
        return;
      }

      foreach (var device in devices)
      {
        device.IsOnline = false;
        _cache.AddOrUpdate(device.Id, device, (_, _) => device);
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while trying to load device cache from file system.");
    }
    finally
    {
      _fileLock.Release();
    }
  }

  public async Task Remove(DeviceDto device)
  {
    if (_cache.Remove(device.Id, out _))
    {
      await TrySaveCache();
    }
  }

  public async Task SetAllOffline()
  {
    foreach (var device in _cache.Values)
    {
      device.IsOnline = false;
    }

    await TrySaveCache();
  }

  public bool TryGet(Guid deviceId, [NotNullWhen(true)] out DeviceDto? device)
  {
    return _cache.TryGetValue(deviceId, out device);
  }

  private Task TrySaveCache()
  {
    Debouncer.Debounce(
      TimeSpan.FromSeconds(3),
      async () =>
      {
        await _fileLock.WaitAsync();
        try
        {
          if (!fileIo.FileExists(_deviceCachePath))
          {
            fileIo.CreateFile(_deviceCachePath).Close();
          }

          var json = JsonSerializer.Serialize(_cache.Values);
          fileIo.WriteAllText(_deviceCachePath, json);
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error while trying to save device cache to file system.");
        }
        finally
        {
          _fileLock.Release();
        }
      });
    return Task.CompletedTask;
  }
}