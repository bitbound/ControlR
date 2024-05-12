using ControlR.Shared.Dtos;
using ControlR.Shared.Models;

namespace ControlR.Shared.Interfaces.HubClients;

public interface IViewerHubClient : IHubClient
{
    Task ReceiveAlertBroadcast(AlertBroadcastDto alert);

    Task ReceiveDesktopChanged(Guid sessionId);

    Task ReceiveDeviceUpdate(DeviceDto device);

    Task ReceiveIceCandidate(Guid sessionId, string candidateJson);

    Task ReceiveStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);

    Task ReceiveRtcSessionDescription(Guid sessionId, RtcSessionDescription sessionDescription);

    Task ReceiveServerStats(ServerStatsDto serverStats);

    Task ReceiveTerminalOutput(TerminalOutputDto output);
}