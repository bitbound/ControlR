using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;

namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IViewerHubClient : IHubClient
{
    Task ReceiveAlertBroadcast(AlertBroadcastDto alert);
    Task ReceiveDeviceUpdate(DeviceDto device);
    Task ReceiveServerStats(ServerStatsDto serverStats);
    Task ReceiveStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
    Task ReceiveTerminalOutput(TerminalOutputDto output);
}