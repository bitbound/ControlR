using ControlR.Libraries.Shared.Hubs.Clients;

namespace ControlR.Web.Client.Services;

public class MainBrowserHubClient(
  IMessenger messenger,
  IDeviceStore deviceStore) 
  : BrowserHubClientBase(messenger, deviceStore), IMainBrowserHubClient
{
  public async Task ReceiveServerStats(ServerStatsDto serverStats)
  {
    var message = new DtoReceivedMessage<ServerStatsDto>(serverStats);
    await Messenger.Send(message);
  }
}
