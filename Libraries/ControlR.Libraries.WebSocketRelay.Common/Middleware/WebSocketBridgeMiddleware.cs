using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;

namespace ControlR.Libraries.WebSocketRelay.Common.Middleware;

internal class WebSocketBridgeMiddleware(
    RequestDelegate _next,
    IHostApplicationLifetime _appLifetime,
    ISessionStore _streamStore,
    ILogger<WebSocketBridgeMiddleware> _logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
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

        if (parts.Length < 3)
        {
            SetBadRequest(context, "Path should have at least 3 parts.");
            return;
        }

        var sessionParam = parts[^2];
        var accessToken = parts[^1];

        if (!Guid.TryParse(sessionParam, out var sessionId))
        {
            SetBadRequest(context, "Session ID is not a valid GUID.");
            return;
        }

        var requestId = Guid.NewGuid();
        await using var signaler = _streamStore.GetOrAdd(sessionId, id => new SessionSignaler(requestId, accessToken));

        if (!signaler.ValidateToken(accessToken))
        {
            _logger.LogError("Invalid access token.  Session ID: {SessionId}", sessionId);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.CompleteAsync();
            return;
        }

        if (!signaler.SignalReady())
        {
            _logger.LogError("Failed to signal ready.  Session ID: {SessionId}", sessionId);
            SetBadRequest(context, "Failed to signal ready.");
            return;
        }

        try
        {
            await signaler.WaitForPartner(_appLifetime.ApplicationStopping);
            _ = _streamStore.TryRemove(sessionId, out _);

            var websocket = await context.WebSockets.AcceptWebSocketAsync();
            await signaler.SetWebsocket(websocket, requestId, _appLifetime.ApplicationStopping);
            await StreamToPartner(signaler, requestId);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Timed out while waiting for partner to connect.");
            context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
            await context.Response.WriteAsync("Timed out while waiting for partner.");
            return;
        }
    }

    private void SetBadRequest(HttpContext context, string message)
    {
        _logger.LogWarning("Bad request.  Path: {RequestPath}", context.Request.Path);
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

            _logger.LogInformation("Starting stream bridge.  Request ID: {RequestId}", callerRequestId);

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
        catch (OperationCanceledException)
        { 
            _logger.LogInformation("Application shutting down.  Streaming aborted.");
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
