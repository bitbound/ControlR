using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;

namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IViewerHubClient : IHubClient
{
    Task ReceiveAlertBroadcast(AlertBroadcastDto alert);
    Task ReceiveClipboardChanged(ClipboardChangeDto clipboardChangeDto);

    Task ReceiveDeviceUpdate(DeviceDto device);
    Task ReceiveServerStats(ServerStatsDto serverStats);
    Task ReceiveStreamerDisconnected(Guid sessionId);
    Task ReceiveStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
    Task ReceiveStreamerInitData(StreamerInitDataDto streamerInitData);

    Task ReceiveTerminalOutput(TerminalOutputDto output);
}