using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Server.Models;
using ControlR.Server.Services;
using System.Net.WebSockets;

namespace ControlR.Server.Middleware;

public class StreamerProxyMiddleware(
    RequestDelegate _next,
    IHostApplicationLifetime _appLifetime,
    IMemoryProvider _memoryProvider,
    IStreamingSessionStore _streamStore,
    ILogger<StreamerProxyMiddleware> _logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        var websocket = await context.WebSockets.AcceptWebSocketAsync();

        if (!context.Request.Path.StartsWithSegments("/streamer-ws-endpoint"))
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
        }
    }

    private async Task StreamToViewer(WebSocket streamerWebsocket, StreamSignaler storedSignaler)
    {
        await using var signaler = storedSignaler;
        signaler.StreamerWebsocket = streamerWebsocket;
        signaler.SignalStreamerReady();

        using var signalExpiration = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await signaler.WaitForViewer(signalExpiration.Token);

        Guard.IsNotNull(signaler.ViewerWebsocket);

        using var buffer = _memoryProvider.CreateEphemeralBuffer<byte>(ushort.MaxValue);
        var bufferMemory = buffer.Value.AsMemory();
        try
        {
            while (streamerWebsocket.State == WebSocketState.Open &&
                signaler.ViewerWebsocket.State == WebSocketState.Open &&
                !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var result = await streamerWebsocket.ReceiveAsync(bufferMemory, _appLifetime.ApplicationStopping);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Websocket close message received.");
                    break;
                }

                if (result.Count == 0)
                {
                    continue;
                }
                await signaler.ViewerWebsocket.SendAsync(
                    bufferMemory[..result.Count],
                    WebSocketMessageType.Binary,
                    true,
                    _appLifetime.ApplicationStopping);
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode is WebSocketError.InvalidState or WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogInformation("Viewer websocket closed.  Ending stream.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while proxying agent websocket.");
        }
    }
}