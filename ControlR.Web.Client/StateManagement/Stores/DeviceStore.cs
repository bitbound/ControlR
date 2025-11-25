using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.StateManagement.Stores;

public interface IDeviceStore : IStoreBase<DeviceDto>
{ }

internal class DeviceStore : StoreBase<DeviceDto>, IDeviceStore
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

  protected override async Task RefreshImpl()
  {
    await foreach (var device in ControlrApi.GetAllDevices())
    {
      Cache.AddOrUpdate(device.Id, device, (_, _) => device);
    }
  }

  private async Task HandleHubConnectionStateChanged(object subscriber, HubConnectionStateChangedMessage message)
  {
    if (message.NewState == HubConnectionState.Connected)
    {
      await Refresh();
    }
  }
}