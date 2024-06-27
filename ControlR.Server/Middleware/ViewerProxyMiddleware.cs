using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Server.Models;
using ControlR.Server.Services;
using System.Net.WebSockets;

namespace ControlR.Server.Middleware;

public class ViewerProxyMiddleware(
    RequestDelegate _next,
    IHostApplicationLifetime _appLifetime,
    IStreamingSessionStore _streamStore,
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

        if (!context.Request.Path.StartsWithSegments("/viewer-ws-endpoint"))
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
            await StreamToStreamer(websocket, storedSignaler);
        }
        finally
        {
            _ = _streamStore.TryRemove(sessionId, out _);
        }
    }

    private async Task StreamToStreamer(WebSocket viewerWebsocket, StreamSignaler storedSignaler)
    {
        try
        {
            await using var signaler = storedSignaler;
            storedSignaler.ViewerWebsocket = viewerWebsocket;

            signaler.SignalViewerReady();

            using var signalExpiration = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            await signaler.WaitForStreamer(signalExpiration.Token);

            Guard.IsNotNull(signaler.StreamerWebsocket);

            using var buffer = _memoryProvider.CreateEphemeralBuffer<byte>(ushort.MaxValue);
            var bufferMemory = buffer.Value.AsMemory();

            while (
                viewerWebsocket.State == WebSocketState.Open &&
                signaler.StreamerWebsocket.State == WebSocketState.Open &&
                 !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var result = await viewerWebsocket.ReceiveAsync(bufferMemory, _appLifetime.ApplicationStopping);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Websocket close message received.");
                    break;
                }

                if (result.Count == 0)
                {
                    continue;
                }

                await signaler.StreamerWebsocket.SendAsync(
                    bufferMemory[..result.Count],
                    WebSocketMessageType.Binary,
                    true,
                    _appLifetime.ApplicationStopping);
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode is WebSocketError.InvalidState or WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogInformation("Streamer websocket closed.  Ending stream.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying viewer websocket.");
        }
    }
}