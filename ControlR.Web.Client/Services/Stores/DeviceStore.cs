using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Services.Stores;

public interface IDeviceStore
{
  ICollection<DeviceResponseDto> Devices { get; }
  void AddOrUpdate(DeviceResponseDto device);

  void Clear();
  Task Refresh();
  Task Remove(DeviceResponseDto device);

  Task SetAllOffline();
  bool TryGet(Guid deviceId, [NotNullWhen(true)] out DeviceResponseDto? device);
}

internal class DeviceStore : IDeviceStore
{
  private readonly ConcurrentDictionary<Guid, DeviceResponseDto> _cache = new();
  private readonly IControlrApi _controlrApi;
  private readonly ILogger<DeviceStore> _logger;
  private readonly IMessenger _messenger;

  private readonly SemaphoreSlim _refreshLock = new(1, 1);
  private readonly ISnackbar _snackbar;

  public DeviceStore(
    IControlrApi controlrApi,
    ISnackbar snackbar,
    IMessenger messenger,
    ILogger<DeviceStore> logger)
  {
    _controlrApi = controlrApi;
    _snackbar = snackbar;
    _messenger = messenger;
    _logger = logger;

    messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChanged);
  }

  public ICollection<DeviceResponseDto> Devices => _cache.Values;

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
      // If another thread already acquired the lock, we still want to wait
      // for it to finish, but we don't want to do another refresh.
      await _refreshLock.WaitAsync();
      _refreshLock.Release();
      return;
    }

    try
    {
      await SetAllOffline();
      await foreach (var device in _controlrApi.GetAllDevices())
      {
        _cache.AddOrUpdate(device.Id, device, (_, _) => device);
      }

      await _messenger.SendGenericMessage(GenericMessageKind.DeviceStoreUpdated);
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