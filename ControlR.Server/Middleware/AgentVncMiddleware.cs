using ControlR.Server.Services;
using ControlR.Shared.Helpers;
using System.Buffers;
using System.Net.WebSockets;

namespace ControlR.Server.Middleware;

public class AgentVncMiddleware(
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

        if (!context.Request.Path.StartsWithSegments("/agentvnc-proxy"))
        {
            await _next(context);
            return;
        }

        var sessionParam = context.Request.Path.Value?.Split("/").Last();

        if (!Guid.TryParse(sessionParam, out var sessionId) ||
            !_proxyStreamStore.TryGet(sessionId, out var signaler))
        {
            await _next(context);
            return;
        }

        signaler.AgentVncWebsocket = websocket;
        signaler.AgentVncReady.Release();

        using var signalExpiration = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await signaler.NoVncViewerReady.WaitAsync(signalExpiration.Token);

        Guard.IsNotNull(signaler.NoVncWebsocket);

        var buffer = ArrayPool<byte>.Shared.Rent(ushort.MaxValue);

        try
        {
            while (signaler.AgentVncWebsocket.State == WebSocketState.Open &&
                signaler.NoVncWebsocket.State == WebSocketState.Open &&
                !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var result = await signaler.AgentVncWebsocket.ReceiveAsync(buffer, _appLifetime.ApplicationStopping);
                if (result.Count == 0)
                {
                    continue;
                }

                await signaler.NoVncWebsocket.SendAsync(
                    buffer.AsMemory()[..result.Count],
                    WebSocketMessageType.Binary,
                    true,
                    _appLifetime.ApplicationStopping);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            _ = _proxyStreamStore.TryRemove(sessionId, out _);

            if (signaler.AgentVncWebsocket.State == WebSocketState.Open)
            {
                await signaler.AgentVncWebsocket.SendAsync(
                    Array.Empty<byte>(),
                    WebSocketMessageType.Close,
                    true,
                    _appLifetime.ApplicationStopping);
            }
        }
    }
}