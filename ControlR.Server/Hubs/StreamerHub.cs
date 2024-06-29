using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Server.Models;
using ControlR.Server.Services;
using ControlR.Server.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Server.Hubs;


public class StreamerHub(
    IHubContext<ViewerHub, IViewerHubClient> _viewerHub,
    IConnectionCounter _connectionCounter,
    ILogger<StreamerHub> _logger) : HubWithItems<IStreamerHubClient>, IStreamerHub
{

    private Guid SessionId
    {
        get => GetItem(Guid.Empty);
        set => SetItem(value);
    }

    private string ViewerConnectionId
    {
        get => GetItem(string.Empty);
        set => SetItem(value);
    }
    public override async Task OnConnectedAsync()
    {
        _connectionCounter.IncrementStreamerCount();
        await SendUpdatedConnectionCountToAdmins();
        await base.OnConnectedAsync();
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _connectionCounter.DecrementStreamerCount();
            await SendUpdatedConnectionCountToAdmins();

            if (!string.IsNullOrEmpty(ViewerConnectionId))
            {
                await _viewerHub.Clients
                    .Client(ViewerConnectionId)
                    .ReceiveStreamerDisconnected(SessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending streamer disconnect notification.");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendClipboardChangeToViewer(string viewerConnectionId, ClipboardChangeDto clipboardChangeDto)
    {
        try
        {
            await _viewerHub.Clients.Client(viewerConnectionId).ReceiveClipboardChanged(clipboardChangeDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending clipboard change to viewer.");
        }
    }

    public async Task SendStreamerInitDataToViewer(
        string viewerConnectionId,
        StreamerInitDataDto streamerInit)
    {
        ViewerConnectionId = viewerConnectionId;
        SessionId = streamerInit.SessionId;

        await _viewerHub.Clients
            .Client(viewerConnectionId)
            .ReceiveStreamerInitData(streamerInit);
    }

    private async Task SendUpdatedConnectionCountToAdmins()
    {
        try
        {
            var agentResult = await _connectionCounter.GetAgentConnectionCount();
            var viewerResult = await _connectionCounter.GetViewerConnectionCount();
            var streamerResult = await _connectionCounter.GetStreamerConnectionCount();

            if (!agentResult.IsSuccess)
            {
                _logger.LogResult(agentResult);
                return;
            }

            if (!viewerResult.IsSuccess)
            {
                _logger.LogResult(viewerResult);
                return;
            }

            if (!streamerResult.IsSuccess)
            {
                _logger.LogResult(streamerResult);
                return;
            }


            var dto = new ServerStatsDto(
                agentResult.Value,
                viewerResult.Value,
                streamerResult.Value);

            await _viewerHub.Clients
                .Group(HubGroupNames.ServerAdministrators)
                .ReceiveServerStats(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending updated agent connection count to admins.");
        }
    }
}
