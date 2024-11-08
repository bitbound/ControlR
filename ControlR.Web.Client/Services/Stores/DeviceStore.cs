﻿using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Services.Stores;

public interface IDeviceStore : IStoreBase<DeviceResponseDto>
{
  Task SetAllOffline();
}

internal class DeviceStore : StoreBase<DeviceResponseDto>, IDeviceStore
{
  public DeviceStore(
    IControlrApi controlrApi,
    ISnackbar snackbar,
    IMessenger messenger,
    ILogger<DeviceStore> logger)
    : base(controlrApi, snackbar, logger)
  {
    messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChanged);
  }

  public Task SetAllOffline()
  {
    foreach (var device in Cache.Values)
    {
      device.IsOnline = false;
    }

    return Task.CompletedTask;
  }


  private async Task HandleHubConnectionStateChanged(object subscriber, HubConnectionStateChangedMessage message)
  {
    if (message.NewState == HubConnectionState.Connected)
    {
      await Refresh();
    }
  }

  protected override async Task RefreshImpl()
  {
    await SetAllOffline();
    await foreach (var device in ControlrApi.GetAllDevices())
    {
      Cache.AddOrUpdate(device.Id, device, (_, _) => device);
    }
  }
}