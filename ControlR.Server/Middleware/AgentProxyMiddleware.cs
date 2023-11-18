using ControlR.Server.Services;
using ControlR.Shared.Helpers;
using ControlR.Shared.Services.Buffers;
using System.Net.WebSockets;

namespace ControlR.Server.Middleware;

public class AgentProxyMiddleware(
    RequestDelegate _next,
    IHostApplicationLifetime _appLifetime,
    IMemoryProvider _memoryProvider,
    ILogger<AgentProxyMiddleware> _logger,
    IProxyStreamStore _proxyStreamStore)
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
            !_proxyStreamStore.TryGet(sessionId, out var storedSignaler))
        {
            await _next(context);
            return;
        }

        await using var signaler = storedSignaler;
        signaler.AgentVncWebsocket = websocket;
        signaler.AgentVncReady.Release();

        using var signalExpiration = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await signaler.NoVncViewerReady.WaitAsync(signalExpiration.Token);

        _ = _proxyStreamStore.TryRemove(sessionId, out _);

        Guard.IsNotNull(signaler.NoVncWebsocket);

        using var buffer = _memoryProvider.CreateEphemeralBuffer<byte>(ushort.MaxValue);
        try
        {
            while (websocket.State == WebSocketState.Open &&
                signaler.NoVncWebsocket.State == WebSocketState.Open &&
                !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var result = await websocket.ReceiveAsync(buffer.Value, _appLifetime.ApplicationStopping);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Websocket close message received.");
                    break;
                }

                if (result.Count == 0)
                {
                    continue;
                }

                await signaler.NoVncWebsocket.SendAsync(
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