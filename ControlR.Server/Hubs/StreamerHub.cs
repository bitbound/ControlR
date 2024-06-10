using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Models;
using ControlR.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Server.Hubs;

public class StreamerHub(
    IHubContext<ViewerHub, IViewerHubClient> _viewerHub,
    IIceServerProvider _iceProvider,
    ILogger<StreamerHub> _logger) : Hub<IStreamerHubClient>
{
    public async Task SendStreamerInitDataToViewer(
        string viewerConnectionId, 
        StreamerInitDataDto streamerInit)
    {
        await _viewerHub.Clients
            .Client(viewerConnectionId)
            .ReceiveStreamerInitData(streamerInit);
    }

    public async Task<IceServer[]> GetIceServers()
    {
        return await _iceProvider.GetIceServers();
    }

    public async Task NotifyViewerDesktopChanged(string viewerConnectionId, Guid sessionId)
    {
        try
        {
            await _viewerHub.Clients.Client(viewerConnectionId).ReceiveDesktopChanged(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while notifying viewer of desktop change.");
        }
    }


    public async Task SendIceCandidate(string viewerConnectionId, Guid sessionId, string candidateJson)
    {
        await _viewerHub.Clients
        .Client(viewerConnectionId)
        .ReceiveIceCandidate(sessionId, candidateJson);
    }

    public async Task SendRtcSessionDescription(string viewerConnectionId, Guid sessionId, RtcSessionDescription sessionDescription)
    {
        await _viewerHub.Clients
            .Client(viewerConnectionId)
            .ReceiveRtcSessionDescription(sessionId, sessionDescription);
    }
}
