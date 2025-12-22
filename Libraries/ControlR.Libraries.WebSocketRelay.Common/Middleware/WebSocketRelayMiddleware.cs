using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.WebSockets;

namespace ControlR.Libraries.WebSocketRelay.Common.Middleware;

internal class WebSocketRelayMiddleware(
    RequestDelegate _next,
    IHostApplicationLifetime _appLifetime,
    IRelaySessionStore _streamStore,
    ILogger<WebSocketRelayMiddleware> _logger)
{
  private const int BufferSize = 256 * 1024;

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

    if (!context.Request.Query.TryGetValue("sessionId", out var sessionIdValue) ||
        !Guid.TryParse(sessionIdValue, out var sessionId))
    {
      SetBadRequest(context, "Invalid or missing session ID.");
      return;
    }

    if (!context.Request.Query.TryGetValue("accessToken", out var accessTokenParam) ||
        $"{accessTokenParam}" is not { Length: > 0 } accessToken)
    {
      SetBadRequest(context, "Invalid or missing access token.");
      return;
    }

    var requestId = Guid.NewGuid();

    await using var signaler = _streamStore
      .GetOrAdd(sessionId, id => new SessionSignaler(requestId, accessToken));

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

    var waitForPartnerTimeout = TimeSpan.FromSeconds(10);

    if (context.Request.Query.TryGetValue("timeout", out var timeoutValue) &&
        int.TryParse(timeoutValue, out var timeoutSeconds))
    {
      if (timeoutSeconds < 0)
      {
        SetBadRequest(context, "Timeout cannot be negative.");
        return;
      }

      waitForPartnerTimeout = timeoutSeconds switch
      {
        0 => Timeout.InfiniteTimeSpan,
        _ => TimeSpan.FromSeconds(timeoutSeconds),
      };
    }

    try
    {
      using var cts = new CancellationTokenSource(waitForPartnerTimeout);
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, _appLifetime.ApplicationStopping);

      await signaler.WaitForPartner(linkedCts.Token);
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
    _logger.LogWarning("Bad request. Uri: {RequestUri}", context.Request.GetDisplayUrl());
    context.Response.StatusCode = StatusCodes.Status400BadRequest;
    var body = 
      $"{message}\n\nPath should be in the form of " +
      $"'/relay?sessionId={{session-id (Guid)}}&accessToken={{accessToken}}'.";
    context.Response.WriteAsync(body, _appLifetime.ApplicationStopping);
  }

  private async Task StreamToPartner(SessionSignaler signaler, Guid callerRequestId)
  {
    var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
    try
    {
      ArgumentNullException.ThrowIfNull(signaler.Websocket1);
      ArgumentNullException.ThrowIfNull(signaler.Websocket2);

      _logger.LogInformation("Starting stream relay. Request ID: {RequestId}", callerRequestId);

      var partnerWebsocket = signaler.GetPartnerWebsocket(callerRequestId);
      var callerWebsocket = signaler.GetCallerWebsocket(callerRequestId);

      while (!_appLifetime.ApplicationStopping.IsCancellationRequested)
      {
        var result = await callerWebsocket.ReceiveAsync(buffer, _appLifetime.ApplicationStopping);

        if (result.MessageType == WebSocketMessageType.Close)
        {
          _logger.LogInformation("Websocket close message received.");
          break;
        }

        await partnerWebsocket.SendAsync(
            buffer.AsMemory(0, result.Count),
            result.MessageType,
            result.EndOfMessage,
            _appLifetime.ApplicationStopping);
      }
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("Application shutting down. Streaming aborted.");
    }
    catch (WebSocketException ex) when (ex.WebSocketErrorCode is WebSocketError.InvalidState or WebSocketError.ConnectionClosedPrematurely)
    {
      _logger.LogInformation("Streamer websocket closed. Ending stream.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while proxying viewer websocket.");
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(buffer);
    }
  }
}
