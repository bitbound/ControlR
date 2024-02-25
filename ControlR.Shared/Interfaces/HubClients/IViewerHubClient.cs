using ControlR.Shared.Dtos;

namespace ControlR.Shared.Interfaces.HubClients;

public interface IViewerHubClient : IHubClient
{
    Task ReceiveAlertBroadcast(AlertBroadcastDto alert);

    Task ReceiveDeviceUpdate(DeviceDto device);

    Task ReceiveServerStats(ServerStatsDto serverStats);

    Task ReceiveTerminalOutput(TerminalOutputDto output);
}