using ControlR.Server.Hubs;
using ControlR.Server.Models;
using ControlR.Server.Services;
using ControlR.Shared.Dtos;
using ControlR.Shared.Helpers;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Services.Buffers;
using Microsoft.AspNetCore.SignalR;
using System.Net.WebSockets;

namespace ControlR.Server.Middleware;

public class AgentProxyMiddleware(
    RequestDelegate _next,
    IHostApplicationLifetime _appLifetime,
    IMemoryProvider _memoryProvider,
    IConnectionCounter _connectionCounter,
    IProxyStreamStore _streamStore,
    IHubContext<ViewerHub, IViewerHubClient> _viewerHub,
    ILogger<AgentProxyMiddleware> _logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        var websocket = await context.WebSockets.AcceptWebSocketAsync();

        if (!context.Request.Path.StartsWithSegments("/agent-proxy"))
        {
            await _next(context);
            return;
        }

        var sessionParam = context.Request.Path.Value?.Split("/").Last();

        if (!Guid.TryParse(sessionParam, out var sessionId) ||
            !_streamStore.TryGet(sessionId, out var storedSignaler))
        {
            await _next(context);
            return;
        }

        try
        {
            await StreamToViewer(websocket, storedSignaler);
        }
        finally
        {
            _ = _streamStore.TryRemove(sessionId, out _);
            await SendUpdatedConnectionCountToAdmins();
        }
    }

    private async Task SendUpdatedConnectionCountToAdmins()
    {
        try
        {
            var dto = new ServerStatsDto(
                _connectionCounter.AgentCount,
                _connectionCounter.ViewerCount,
                _streamStore.Count);

            await _viewerHub.Clients
                .Group(HubGroupNames.ServerAdministrators)
                .ReceiveServerStats(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending updated agent connection count to admins.");
        }
    }

    private async Task StreamToViewer(WebSocket agentWebsocket, StreamSignaler storedSignaler)
    {
        await using var signaler = storedSignaler;
        signaler.AgentVncWebsocket = agentWebsocket;
        signaler.SignalAgentReady();

        using var signalExpiration = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await signaler.WaitForViewer(signalExpiration.Token);

        Guard.IsNotNull(signaler.ViewerVncWebsocket);

        using var buffer = _memoryProvider.CreateEphemeralBuffer<byte>(ushort.MaxValue);
        try
        {
            while (agentWebsocket.State == WebSocketState.Open &&
                signaler.ViewerVncWebsocket.State == WebSocketState.Open &&
                !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var result = await agentWebsocket.ReceiveAsync(buffer.Value, _appLifetime.ApplicationStopping);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Websocket close message received.");
                    break;
                }

                if (result.Count == 0)
                {
                    continue;
                }

                await signaler.ViewerVncWebsocket.SendAsync(
                    buffer.Value.AsMemory()[..result.Count],
                    WebSocketMessageType.Binary,
                    true,
                    _appLifetime.ApplicationStopping);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying agent websocket.");
        }
    }
}