using ControlR.Server.Models;
using ControlR.Server.Options;
using ControlR.Server.Services;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using ControlR.Shared.Services.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

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
