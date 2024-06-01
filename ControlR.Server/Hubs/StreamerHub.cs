using ControlR.Server.Services;
using ControlR.Server.Services.Interfaces;
using ControlR.Shared.Dtos;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Server.Hubs;

public class StreamerHub(
    IStreamerSessionCache _streamerSessionCache,
    IHubContext<ViewerHub, IViewerHubClient> _viewerHub,
    IIceServerProvider _iceProvider,
    ILogger<StreamerHub> _logger) : Hub<IStreamerHubClient>
{
    public Task SetSessionDetails(Guid sessionId, DisplayDto[] displays)
    {
        var session = new StreamerHubSession(sessionId, displays, Context.ConnectionId);
        _streamerSessionCache.AddOrUpdate(sessionId, session);
        return Task.CompletedTask;
    }

    public async Task<IceServer[]> GetIceServers()
    {
        return await _iceProvider.GetIceServers();
    }

    public async Task NotifyViewerDesktopChanged(Guid sessionId)
    {
        try
        {
            if (!_streamerSessionCache.TryGetValue(sessionId, out var session))
            {
                _logger.LogError("Could not find session ID to notify of desktop change: {id}", sessionId);
                return;
            }

            if (string.IsNullOrWhiteSpace(session.ViewerConnectionId))
            {
                _logger.LogError("Viewer connection ID is unexpectedly empty.");
                return;
            }

            await _viewerHub.Clients.Client(session.ViewerConnectionId).ReceiveDesktopChanged(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while notifying viewer of desktop change.");
        }
    }


    public async Task SendIceCandidate(Guid sessionId, string candidateJson)
    {
        if (!_streamerSessionCache.TryGetValue(sessionId, out var session))
        {
            _logger.LogError("Could not find session for ID {id}.", sessionId);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.ViewerConnectionId))
        {
            _logger.LogError("Viewer's connection ID hasn't been set on the session.");
            return;
        }

        await _viewerHub.Clients.Client(session.ViewerConnectionId).ReceiveIceCandidate(sessionId, candidateJson);
    }

    public async Task SendRtcSessionDescription(Guid sessionId, RtcSessionDescription sessionDescription)
    {
        if (!_streamerSessionCache.TryGetValue(sessionId, out var session))
        {
            _logger.LogError("Could not find session for ID {id}.", sessionId);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.ViewerConnectionId))
        {
            _logger.LogError("Viewer's connection ID hasn't been set on the session.");
            return;
        }

        await _viewerHub.Clients.Client(session.ViewerConnectionId).ReceiveRtcSessionDescription(sessionId, sessionDescription);
    }
}
