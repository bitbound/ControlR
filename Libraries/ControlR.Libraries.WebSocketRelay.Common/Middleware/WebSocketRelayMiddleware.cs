using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.WebSockets;
using ControlR.Libraries.WebSocketRelay.Common.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.WebSocketRelay.Common.Middleware;

internal class WebSocketRelayMiddleware(
    RequestDelegate next,
    IHostApplicationLifetime appLifetime,
    IRelaySessionStore streamStore,
    IServiceProvider serviceProvider,
    IOptions<WebSocketRelayOptions> relayOptions,
    ILogger<WebSocketRelayMiddleware> logger)
{
  private const int BufferSize = 256 * 1024;
  private readonly TimeSpan _defaultWaitForPartnerTimeout =  TimeSpan.FromSeconds(20);

  public async Task InvokeAsync(HttpContext context)
  {
    if (!context.WebSockets.IsWebSocketRequest)
    {
      await next(context);
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

    if (!context.Request.Query.TryGetValue("role", out var roleValue) ||
        !Enum.TryParse<RelayRole>(roleValue, true, out var role))
    {
      SetBadRequest(context, "Invalid or missing role.");
      return;
    }

    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
      ["TraceId"] = context.TraceIdentifier,
      ["RequestPath"] = context.Request.Path,
      ["RequestQueryString"] = context.Request.QueryString.ToString(),
      ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
      ["SessionId"] = sessionId,
      ["Role"] = role.ToString()
    });

    using var serviceScope = serviceProvider.CreateScope();
    var authService = serviceScope.ServiceProvider.GetRequiredService<IAuthorizationService>();

    var options = relayOptions.Value;
    var requireAuth = role == RelayRole.Requester 
      ? options.RequireAuthenticationForRequester 
      : options.RequireAuthenticationForResponder;

    var policy = role == RelayRole.Requester 
      ? options.AuthorizationPolicyForRequester 
      : options.AuthorizationPolicyForResponder;

    if (requireAuth && context.User.Identity?.IsAuthenticated != true)
    {
      context.Response.StatusCode = StatusCodes.Status401Unauthorized;
      return;
    }

    if (!string.IsNullOrEmpty(policy))
    {
      var result = await authService.AuthorizeAsync(context.User, policy);
      if (!result.Succeeded)
      {
        logger.LogWarning("Authorization failed for policy '{Policy}'.", policy);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
      }
    }

    var peerId = Guid.NewGuid();

    await using var signaler = streamStore
      .GetOrAdd(sessionId, id => new SessionSignaler(accessToken));

    if (!signaler.ValidateToken(accessToken))
    {
      logger.LogError("Invalid access token.  Session ID: {SessionId}", sessionId);
      context.Response.StatusCode = StatusCodes.Status401Unauthorized;
      await context.Response.CompleteAsync();
      return;
    }

    if (!signaler.TryAssignRole(peerId, role))
    {
      logger.LogError("Role already assigned. Session ID: {SessionId}, Role: {Role}", sessionId, role);
      SetBadRequest(context, $"Role '{role}' is already assigned for this session.");
      return;
    }

    if (!signaler.SignalReady(role))
    {
      logger.LogError("Failed to signal ready.  Session ID: {SessionId}", sessionId);
      SetBadRequest(context, "Failed to signal ready.");
      return;
    }

    var waitForPartnerTimeout = _defaultWaitForPartnerTimeout;

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
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, appLifetime.ApplicationStopping);

      await signaler.WaitForPartner(linkedCts.Token);
      _ = streamStore.TryRemove(sessionId, out _);

      var websocket = await context.WebSockets.AcceptWebSocketAsync();
      await signaler.SetWebsocket(websocket, peerId, appLifetime.ApplicationStopping);
      await StreamToPartner(signaler, peerId);
    }
    catch (OperationCanceledException ex)
    {
      logger.LogWarning(ex, "Timed out while waiting for partner to connect.");
      context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
      await context.Response.WriteAsync("Timed out while waiting for partner.");
      return;
    }
  }

  private void SetBadRequest(HttpContext context, string message)
  {
    logger.LogWarning(
      "Bad request. Uri: {RequestUri}. Message: {Message}", 
      context.Request.GetDisplayUrl(),
      message);

    context.Response.StatusCode = StatusCodes.Status400BadRequest;
    var body = 
      $"{message}\n\nPath should be in the form of " +
      $"'/relay?sessionId={{session-id (Guid)}}&accessToken={{accessToken}}&role={{requester|responder}}'.";
    context.Response.WriteAsync(body, appLifetime.ApplicationStopping);
  }

  private async Task StreamToPartner(SessionSignaler signaler, Guid callerPeerId)
  {
    var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
    try
    {
      ArgumentNullException.ThrowIfNull(signaler.RequesterWebsocket);
      ArgumentNullException.ThrowIfNull(signaler.ResponderWebsocket);

      logger.LogInformation("Starting stream relay. Peer ID: {PeerId}", callerPeerId);

      var partnerWebsocket = signaler.GetPartnerWebsocket(callerPeerId);
      var callerWebsocket = signaler.GetCallerWebsocket(callerPeerId);

      while (!appLifetime.ApplicationStopping.IsCancellationRequested)
      {
        var result = await callerWebsocket.ReceiveAsync(buffer, appLifetime.ApplicationStopping);

        if (result.MessageType == WebSocketMessageType.Close)
        {
          logger.LogInformation("Websocket close message received.");
          break;
        }

        await partnerWebsocket.SendAsync(
            buffer.AsMemory(0, result.Count),
            result.MessageType,
            result.EndOfMessage,
            appLifetime.ApplicationStopping);
      }
    }
    catch (OperationCanceledException)
    {
      logger.LogInformation("Application shutting down. Streaming aborted.");
    }
    catch (WebSocketException ex) when (ex.WebSocketErrorCode is WebSocketError.InvalidState or WebSocketError.ConnectionClosedPrematurely)
    {
      logger.LogInformation("Streamer websocket closed. Ending stream.");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while proxying viewer websocket.");
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(buffer);
    }
  }
}
