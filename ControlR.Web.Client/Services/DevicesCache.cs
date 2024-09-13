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

    bool TryGet(string deviceId, [NotNullWhen(true)] out DeviceDto? device);
}

internal class DeviceCache : IDeviceCache
{
    private static readonly ConcurrentDictionary<string, DeviceDto> _cache = new();
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly string _deviceCachePath;
    private readonly ILogger<DeviceCache> _logger;

    public DeviceCache(ILogger<DeviceCache> logger)
    {
        _logger = logger;
    }

    public IEnumerable<DeviceDto> Devices => _cache.Values;

    public async Task AddOrUpdate(DeviceDto device)
    {
        _cache.AddOrUpdate(device.Id, device, (k, v) => device);
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
            _logger.LogError(ex, "Error while trying to load device cache from file system.");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task Remove(DeviceDto device)
    {
        _cache.Remove(device.Id, out _);
    }

    public async Task SetAllOffline()
    {
        foreach (var device in _cache.Values)
        {
            device.IsOnline = false;
        }
    }

    public bool TryGet(string deviceId, [NotNullWhen(true)] out DeviceDto? device)
    {
        return _cache.TryGetValue(deviceId, out device);
    }
}