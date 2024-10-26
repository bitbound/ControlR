using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Client.Services;

public interface IDeviceCache
{
  IEnumerable<DeviceResponseDto> Devices { get; }
  void AddOrUpdate(DeviceResponseDto device);

  void Clear();
  Task Remove(DeviceResponseDto device);

  Task SetAllOffline();
  Task Refresh();
  bool TryGet(Guid deviceId, [NotNullWhen(true)] out DeviceResponseDto? device);
}

internal class DeviceCache(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<DeviceCache> logger) : IDeviceCache
{
  private static readonly ConcurrentDictionary<Guid, DeviceResponseDto> _cache = new();

  private readonly ISnackbar _snackbar = snackbar;
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<DeviceCache> _logger = logger;

  public IEnumerable<DeviceResponseDto> Devices => _cache.Values;

  public void AddOrUpdate(DeviceResponseDto device)
  {
    _cache.AddOrUpdate(device.Id, device, (_, _) => device);
  }

  public void Clear()
  {
    _cache.Clear();
  }


  public async Task Refresh()
  {
    try
    {
      await SetAllOffline();
      await foreach (var device in _controlrApi.GetAllDevices())
      {
        _cache.AddOrUpdate(device.Id, device, (_, _) => device);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while refreshing devices.");
      _snackbar.Add("Failed to load devices", Severity.Error);
    }
  }

  public Task Remove(DeviceResponseDto device)
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

  public bool TryGet(Guid deviceId, [NotNullWhen(true)] out DeviceResponseDto? device)
  {
    return _cache.TryGetValue(deviceId, out device);
  }
}