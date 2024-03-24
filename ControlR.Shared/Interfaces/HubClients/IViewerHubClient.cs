using ControlR.Shared.Dtos;
using ControlR.Shared.Models;

namespace ControlR.Shared.Interfaces.HubClients;

public interface IViewerHubClient : IHubClient
{
    Task ReceiveAlertBroadcast(AlertBroadcastDto alert);

    Task ReceiveDesktopChanged(Guid sessionId, string desktopName);

    Task ReceiveDeviceUpdate(DeviceDto device);

    Task ReceiveIceCandidate(Guid sessionId, string candidateJson);

    Task ReceiveRemoteControlDownloadProgress(Guid streamingSessionId, double downloadProgress);

    Task ReceiveRtcSessionDescription(Guid sessionId, RtcSessionDescription sessionDescription);

    Task ReceiveServerStats(ServerStatsDto serverStats);

    Task ReceiveTerminalOutput(TerminalOutputDto output);
}