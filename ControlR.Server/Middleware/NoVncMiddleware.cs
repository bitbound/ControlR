using ControlR.Server.Services;
using ControlR.Shared.Helpers;
using System.Buffers;
using System.Net.WebSockets;

namespace ControlR.Server.Middleware;

public class NoVncMiddleware(
    RequestDelegate next,
    IHostApplicationLifetime appLifetime,
    IProxyStreamStore proxyStreamStore)
{
    private readonly IHostApplicationLifetime _appLifetime = appLifetime;
    private readonly RequestDelegate _next = next;
    private readonly IProxyStreamStore _proxyStreamStore = proxyStreamStore;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        var websocket = await context.WebSockets.AcceptWebSocketAsync();

        if (!context.Request.Path.StartsWithSegments("/novnc-proxy"))
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

        using var signaler = storedSignaler;

        signaler.NoVncWebsocket = websocket;
        signaler.NoVncViewerReady.Release();

        using var signalExpiration = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await signaler.AgentVncReady.WaitAsync(signalExpiration.Token);

        _ = _proxyStreamStore.TryRemove(sessionId, out _);

        Guard.IsNotNull(signaler.AgentVncWebsocket);

        var buffer = ArrayPool<byte>.Shared.Rent(ushort.MaxValue);

        try
        {
            while (signaler.AgentVncWebsocket.State == WebSocketState.Open &&
             signaler.NoVncWebsocket.State == WebSocketState.Open &&
             !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var result = await signaler.NoVncWebsocket.ReceiveAsync(buffer, _appLifetime.ApplicationStopping);
                if (result.Count == 0)
                {
                    continue;
                }

                await signaler.AgentVncWebsocket.SendAsync(
                    buffer.AsMemory()[0..result.Count],
                    WebSocketMessageType.Binary,
                    true,
                    _appLifetime.ApplicationStopping);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            if (signaler.NoVncWebsocket.State == WebSocketState.Open)
            {
                await signaler.NoVncWebsocket.SendAsync(
                    Array.Empty<byte>(),
                    WebSocketMessageType.Close,
                    true,
                    _appLifetime.ApplicationStopping);
            }
        }
    }
}