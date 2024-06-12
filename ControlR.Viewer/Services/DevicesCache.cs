using Microsoft.Extensions.Logging;
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

    Task Remove(DeviceDto device);

    Task SetAllOffline();

    bool TryGet(string deviceId, [NotNullWhen(true)] out DeviceDto? device);
}

internal class DeviceCache : IDeviceCache
{
    private static readonly ConcurrentDictionary<string, DeviceDto> _cache = new();
    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly string _deviceCachePath;
    private readonly IFileIo _fileIo;
    private readonly ILogger<DeviceCache> _logger;

    public DeviceCache(IFileSystem fileSystem, IFileIo fileIo, ILogger<DeviceCache> logger)
    {
        _fileIo = fileIo;
        _deviceCachePath = Path.Combine(fileSystem.AppDataDirectory, "DeviceCache.json");
        _logger = logger;

        Task.Run(TryLoadCache).Forget();
    }

    public IEnumerable<DeviceDto> Devices => _cache.Values;

    public async Task AddOrUpdate(DeviceDto device)
    {
        _cache.AddOrUpdate(device.Id, device, (k, v) => device);
        await TrySaveCache();
    }

    public void Clear()
    {
        _cache.Clear();
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

    public bool TryGet(string deviceId, [NotNullWhen(true)] out DeviceDto? device)
    {
        return _cache.TryGetValue(deviceId, out device);
    }

    private async Task TryLoadCache()
    {
        await _fileLock.WaitAsync();
        try
        {
            _cache.Clear();

            if (!_fileIo.FileExists(_deviceCachePath))
            {
                _fileIo.CreateFile(_deviceCachePath).Close();
            }

            var content = _fileIo.ReadAllText(_deviceCachePath);

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
                _cache.AddOrUpdate(device.Id, device, (k, v) => device);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while trying to load device cache from file system.");
        }
        finally
        {
            _fileLock.Release();
        }
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
                    if (!_fileIo.FileExists(_deviceCachePath))
                    {
                        _fileIo.CreateFile(_deviceCachePath).Close();
                    }

                    var json = JsonSerializer.Serialize(_cache.Values);
                    _fileIo.WriteAllText(_deviceCachePath, json);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while trying to save device cache to file system.");
                }
                finally
                {
                    _fileLock.Release();
                }
            });
        return Task.CompletedTask;
    }
}