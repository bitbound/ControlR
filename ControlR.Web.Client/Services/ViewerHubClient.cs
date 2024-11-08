using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Web.Client.Services.Stores;

namespace ControlR.Web.Client.Services;

public class ViewerHubClient(IMessenger messenger, IDeviceStore deviceStore) : IViewerHubClient
{
  public async Task ReceiveDeviceUpdate(DeviceResponseDto device)
  {
    deviceStore.AddOrUpdate(device);
    await messenger.SendGenericMessage(GenericMessageKind.DeviceStoreUpdated);
  }

  public async Task ReceiveDto(DtoWrapper dto)
  {
    await messenger.Send(new DtoReceivedMessage<DtoWrapper>(dto));
  }

  public async Task ReceiveServerStats(ServerStatsDto serverStats)
  {
    var message = new DtoReceivedMessage<ServerStatsDto>(serverStats);
    await messenger.Send(message);
  }


  public async Task ReceiveStreamerDownloadProgress(StreamerDownloadProgressDto progressDto)
  {
    var message = new DtoReceivedMessage<StreamerDownloadProgressDto>(progressDto);
    await messenger.Send(message);
  }


  public async Task ReceiveTerminalOutput(TerminalOutputDto output)
  {
    await messenger.Send(new DtoReceivedMessage<TerminalOutputDto>(output));
  }
}
