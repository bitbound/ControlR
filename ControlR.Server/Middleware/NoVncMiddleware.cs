using ControlR.Server.Services;

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
            !_proxyStreamStore.TryGet(sessionId, out var signaler))
        {
            await _next(context);
            return;
        }

        signaler.NoVncWebsocket = websocket;
        // Two threads will be waiting.
        signaler.ReadySignal.Release();
        signaler.ReadySignal.Release();
        await signaler.EndSignal.WaitAsync(_appLifetime.ApplicationStopping);
    }
}