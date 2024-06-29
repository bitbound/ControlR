using ControlR.Server.Models;
using ControlR.Server.Services;
using System.Net.WebSockets;

namespace ControlR.Server.Middleware;

public class WebSocketBridgeMiddleware(
    RequestDelegate _next,
    IHostApplicationLifetime _appLifetime,
    ISessionStore _sessionStore,
    ILogger<WebSocketBridgeMiddleware> _logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Path.StartsWithSegments("/bridge"))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(context.Request.Path.Value))
        {
            SetBadRequest(context, "Path cannot be empty.");
            return;
        }

        var parts = context.Request.Path.Value.Split("/", StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            SetBadRequest(context, "Path should have 2 parts.");
            return;
        }


        var sessionParam = parts[1];

        if (!Guid.TryParse(sessionParam, out var sessionId))
        {
            SetBadRequest(context, "Session ID is not a valid GUID.");
            return;
        }

        var requestId = Guid.NewGuid();
        await using var signaler = _sessionStore.GetOrAdd(sessionId, id => new SessionSignaler(requestId));

        if (!signaler.SignalReady())
        {
            _logger.LogError("Failed to signal ready.  Session ID: {SessionId}", sessionId);
            SetBadRequest(context, "Failed to signal ready.");
            return;
        }

        await signaler.WaitForPartner(_appLifetime.ApplicationStopping);
        _ = _sessionStore.TryRemove(sessionId, out _);

        var websocket = await context.WebSockets.AcceptWebSocketAsync();
        await signaler.SetWebsocket(websocket, requestId, _appLifetime.ApplicationStopping);
        await StreamToPartner(signaler, requestId);
    }

    private void SetBadRequest(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        var body = $"{message}\n\nPath should be in the form of '/bridge/{{session-id (Guid)}}'.";
        context.Response.WriteAsync(body, _appLifetime.ApplicationStopping);
    }

    private async Task StreamToPartner(SessionSignaler signaler, Guid callerRequestId)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(signaler.Websocket1);
            ArgumentNullException.ThrowIfNull(signaler.Websocket2);

            var partnerWebsocket = signaler.GetPartnerWebsocket(callerRequestId);
            var callerWebsocket = signaler.GetCallerWebsocket(callerRequestId);

            var buffer = new byte[ushort.MaxValue];
            var bufferMemory = buffer.AsMemory();

            while (
                partnerWebsocket.State == WebSocketState.Open &&
                callerWebsocket.State == WebSocketState.Open &&
                 !_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var result = await callerWebsocket.ReceiveAsync(bufferMemory, _appLifetime.ApplicationStopping);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Websocket close message received.");
                    break;
                }

                if (result.Count == 0)
                {
                    continue;
                }

                await partnerWebsocket.SendAsync(
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
