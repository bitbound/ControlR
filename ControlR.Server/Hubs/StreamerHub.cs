using ControlR.Server.Models;
using ControlR.Server.Options;
using ControlR.Server.Services;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ControlR.Server.Hubs;

public class StreamerHub(
    IStreamerSessionCache streamerSessionCache,
    IHubContext<ViewerHub, IViewerHubClient> viewerHubContext,
    IOptionsMonitor<ApplicationOptions> appOptions,
    ILogger<StreamerHub> logger) : Hub<IStreamerHubClient>
{
    private readonly IStreamerSessionCache _streamerSessionCache = streamerSessionCache;
    private readonly IOptionsMonitor<ApplicationOptions> _appOptions = appOptions;
    private readonly IHubContext<ViewerHub, IViewerHubClient> _viewerHub = viewerHubContext;
    private readonly ILogger<StreamerHub> _logger = logger;

    public Task SetSessionDetails(Guid sessionId, DisplayDto[] displays)
    {
        var session = new StreamerHubSession(sessionId, displays, Context.ConnectionId);
        _streamerSessionCache.AddOrUpdate(sessionId, session);
        return Task.CompletedTask;
    }

    public Task<IceServer[]> GetIceServers()
    {
        return _appOptions.CurrentValue.IceServers.ToArray().AsTaskResult();
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
