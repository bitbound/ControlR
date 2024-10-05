using Bitbound.SimpleMessenger;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;

namespace ControlR.Web.Client.Services;

public class ViewerHubClient(IMessenger _messenger, IDeviceCache _devicesCache) : IViewerHubClient
{
  public async Task ReceiveAlertBroadcast(AlertBroadcastDto alert)
  {
    await _messenger.Send(new DtoReceivedMessage<AlertBroadcastDto>(alert));
  }

  public async Task ReceiveDeviceUpdate(DeviceDto device)
  {
    _devicesCache.AddOrUpdate(device);
    await _messenger.SendGenericMessage(GenericMessageKind.DevicesCacheUpdated);
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
