using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using IFileIo = ControlR.Devices.Common.Services.IFileSystem;

namespace ControlR.Viewer.Services;

internal interface IDeviceCache
{
    IEnumerable<DeviceDto> Devices { get; }

    Task AddOrUpdate(DeviceDto device);

    Task Remove(DeviceDto device);

    Task SetAllOffline();
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

        Task.Run(TryLoadCache).AndForget();
    }

    public IEnumerable<DeviceDto> Devices => _cache.Values;

    public async Task AddOrUpdate(DeviceDto device)
    {
        _cache.AddOrUpdate(device.Id, device, (k, v) => device);
        await TrySaveCache();
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

    private async Task TrySaveCache()
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
    }
}