using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.Libraries.Shared.Hubs.Clients;

public interface IMainBrowserHubClient : IBrowserHubClientBase
{
  Task ReceiveServerStats(ServerStatsDto serverStats);
}