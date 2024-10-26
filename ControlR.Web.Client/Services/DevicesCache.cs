using ControlR.Libraries.Shared.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
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

internal class DeviceCache : IDeviceCache
{
  private readonly ConcurrentDictionary<Guid, DeviceResponseDto> _cache = new();

  public DeviceCache(
    IControlrApi controlrApi,
    ISnackbar snackbar,
    IMessenger messenger,
    ILogger<DeviceCache> logger)
  {
    _controlrApi = controlrApi;
    _snackbar = snackbar;
    _logger = logger;

    messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChanged);
  }

  private readonly SemaphoreSlim _refreshLock = new(1, 1);
  private readonly ISnackbar _snackbar;
  private readonly IControlrApi _controlrApi;
  private readonly ILogger<DeviceCache> _logger;

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
    if (!await _refreshLock.WaitAsync(0))
    {
      return;
    }

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
    finally
    {
      _refreshLock.Release();
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

  private async Task HandleHubConnectionStateChanged(object subscriber, HubConnectionStateChangedMessage message)
  {
    if (message.NewState == HubConnectionState.Connected)
    {
      await Refresh();
    }
  }
}