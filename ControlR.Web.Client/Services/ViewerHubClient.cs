using Bitbound.SimpleMessenger;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;

namespace ControlR.Web.Client.Services;

public class ViewerHubClient(IMessenger messenger, IDeviceCache deviceCache) : IViewerHubClient
{
  public async Task ReceiveDeviceUpdate(DeviceResponseDto device)
  {
    deviceCache.AddOrUpdate(device);
    await messenger.SendGenericMessage(GenericMessageKind.DevicesCacheUpdated);
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
