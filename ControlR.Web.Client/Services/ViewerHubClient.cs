using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;

namespace ControlR.Web.Client.Services;

public class ViewerHubClient(IMessenger messenger, IDeviceStore deviceStore) : IViewerHubClient
{
  private readonly IMessenger _messenger = messenger;
  private readonly IDeviceStore _deviceStore = deviceStore;

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
