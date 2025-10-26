using ControlR.Libraries.Shared.Hubs.Clients;
using ControlR.Web.Client.Extensions;

namespace ControlR.Web.Client.Services;

public class BrowserHubClientBase(
  IMessenger messenger,
  IDeviceStore deviceStore) : IBrowserHubClientBase
{
  protected readonly IDeviceStore DeviceStore = deviceStore;
  protected readonly IMessenger Messenger = messenger;
  
  public async Task InvokeToast(ToastInfo toastInfo)
  {
    var toastMessage = new ToastMessage(
      toastInfo.Message,
      toastInfo.MessageSeverity.ToMudSeverity());

    await Messenger.Send(toastMessage);
  }

  public async Task ReceiveDeviceUpdate(DeviceDto deviceDto)
  {
    await DeviceStore.AddOrUpdate(deviceDto);
    await Messenger.Send(new DtoReceivedMessage<DeviceDto>(deviceDto));
  }

  public async Task ReceiveDto(DtoWrapper dto)
  {
    await Messenger.Send(new DtoReceivedMessage<DtoWrapper>(dto));
  }
}