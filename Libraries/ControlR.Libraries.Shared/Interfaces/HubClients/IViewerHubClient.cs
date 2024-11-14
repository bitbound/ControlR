using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;

namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IViewerHubClient : IHubClient
{
    Task ReceiveDeviceUpdate(DeviceDto deviceDto);
    Task ReceiveServerStats(ServerStatsDto serverStats);
    Task ReceiveStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
    Task ReceiveTerminalOutput(TerminalOutputDto output);
}