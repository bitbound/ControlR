using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Web.Client.Extensions;

namespace ControlR.Web.Client.Services;

public class ViewerHubClient(IMessenger messenger, IDeviceStore deviceStore) : IViewerHubClient
{
  private readonly IDeviceStore _deviceStore = deviceStore;
  private readonly IMessenger _messenger = messenger;
  public async Task InvokeToast(ToastInfo toastInfo)
  {
    var toastMessage = new ToastMessage(
      toastInfo.Message, 
      toastInfo.MessageSeverity.ToMudSeverity());

    await _messenger.Send(toastMessage);
  }

  public async Task ReceiveDeviceUpdate(DeviceDto deviceDto)
  {
    await _deviceStore.AddOrUpdate(deviceDto);
    await _messenger.Send(new DtoReceivedMessage<DeviceDto>(deviceDto));
  }

  public async Task ReceiveDto(DtoWrapper dto)
  {
    await _messenger.Send(new DtoReceivedMessage<DtoWrapper>(dto));
  }

  public async Task ReceiveServerStats(ServerStatsDto serverStats)
  {
    var message = new DtoReceivedMessage<ServerStatsDto>(serverStats);
    await _messenger.Send(message);
  }


  public async Task ReceiveStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
  {
    var message = new DtoReceivedMessage<StreamerDownloadProgressDto>(progressDto);
    await _messenger.Send(message);
  }


  public async Task ReceiveTerminalOutput(TerminalOutputDto output)
  {
    await _messenger.Send(new DtoReceivedMessage<TerminalOutputDto>(output));
  }
}
