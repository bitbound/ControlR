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

public class ViewerProxyMiddleware(
    RequestDelegate _next,
    IHostApplicationLifetime _appLifetime,
    IConnectionCounter _connectionCounter,
    IHubContext<ViewerHub, IViewerHubClient> _viewerHub,
    IProxyStreamStore _streamStore,
    ILogger<ViewerProxyMiddleware> _logger,
    IMemoryProvider _memoryProvider)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        var websocket = await context.WebSockets.AcceptWebSocketAsync();

        if (!context.Request.Path.StartsWithSegments("/viewer-proxy"))
        {
            await _next(context);
            return;
        }

        var sessionParam = context.Request.Path.Value?.Split("/").Last();

        if (!Guid.TryParse(sessionParam, out var sessionId) ||
            !_streamStore.TryGet(sessionId, out var storedSignaler) ||
            storedSignaler.IsMutallyAcquired)
        {
            await _next(context);
            return;
        }

        try
        {
            await StreamToAgent(websocket, storedSignaler);
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

    private async Task StreamToAgent(WebSocket viewerWebsocket, StreamSignaler storedSignaler)
    {
        try
        {
            await using var signaler = storedSignaler;
            storedSignaler.ViewerVncWebsocket = viewerWebsocket;

            signaler.SignalViewerReady();

            using var signalExpiration = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            await signaler.WaitForAgent(signalExpiration.Token);

            Guard.IsNotNull(signaler.AgentVncWebsocket);

            using var buffer = _memoryProvider.CreateEphemeralBuffer<byte>(ushort.MaxValue);

            while (
                viewerWebsocket.State == WebSocketState.Open &&
                signaler.AgentVncWebsocket.State == WebSocketState.Open &&
                 !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var result = await viewerWebsocket.ReceiveAsync(buffer.Value, _appLifetime.ApplicationStopping);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Websocket close message received.");
                    break;
                }

                if (result.Count == 0)
                {
                    continue;
                }

                await signaler.AgentVncWebsocket.SendAsync(
                    buffer.Value.AsMemory()[0..result.Count],
                    WebSocketMessageType.Binary,
                    true,
                    _appLifetime.ApplicationStopping);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying viewer websocket.");
        }
    }
}