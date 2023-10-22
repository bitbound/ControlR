using ControlR.Shared.Dtos;
using ControlR.Shared.Models;

namespace ControlR.Shared.Interfaces.HubClients;

public interface IViewerHubClient : IHubClient
{
    Task ReceiveDesktopChanged(Guid sessionId, string desktopName);

    Task ReceiveDeviceUpdate(DeviceDto device);

    Task ReceiveIceCandidate(Guid sessionId, string candidateJson);

    Task ReceiveRtcSessionDescription(Guid sessionId, RtcSessionDescription sessionDescription);
}