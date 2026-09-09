using System.Net;
using ControlR.ApiClient.Interfaces.Agent;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.ApiClient;

public interface IControlrApi
{
  IControlrAgentApi Agent { get; }
  IControlrInternalApi Internal { get; }
  IControlrV1Api V1 { get; }
}


public partial class ControlrApi(
  HttpClient httpClient,
  ControlrApiClientAuthState authState,
  IBearerTokenRefresher bearerTokenRefresher,
  ILogger<ControlrApi> logger,
  IOptions<ControlrApiClientOptions> options) : IControlrApi
{
  internal const string DisposedTargetReason =
    "The HTTP client for this target was disposed. Obtain the client again from the factory.";

  private readonly ControlrApiClientAuthState _authState = authState;
  private readonly IBearerTokenRefresher _bearerTokenRefresher = bearerTokenRefresher;
  private readonly HttpClient _client = httpClient;
  private readonly ILogger<ControlrApi> _logger = logger;
  private readonly IOptions<ControlrApiClientOptions> _options = options;

  private AgentApi? _agent;
  private InternalApi? _internal;
  private V1Api? _v1;

  internal AgentApi AgentApi => _agent ??= new(this);
  internal HttpClient HttpClient => _client;
  internal InternalApi InternalApi => _internal ??= new(this);
  internal ILogger<ControlrApi> Logger => _logger;
  internal IOptions<ControlrApiClientOptions> Options => _options;

  /// <summary>
  /// Counts this client's calls so that evicting the target releases its HTTP stack once they
  /// finish. The factory assigns it while building the target. A client nobody evicts keeps the
  /// instance it was given, where acquiring a lease costs a counter pair.
  /// </summary>
  internal InFlightTracker Requests { get; set; } = new();
  internal V1Api V1 => _v1 ??= new(this);

  IControlrAgentApi IControlrApi.Agent => AgentApi;
  IControlrInternalApi IControlrApi.Internal => InternalApi;
  IControlrV1Api IControlrApi.V1 => V1;

  /// <summary>
  /// <para>
  /// Announces a call that <see cref="ExecuteApiCall(Func{Task}, bool)"/> does not wrap, which is how
  /// a streamed response stays counted for as long as its body is still being read. Dispose the
  /// returned lease when the caller is done with the response.
  /// </para>
  /// <para>
  /// The JSON read from a streaming endpoint is buffered by <see cref="HttpClient"/> before it reaches
  /// the caller, so the lease is cleared when that read finishes rather than when the response starts.
  /// A caller that hands items out slowly holds the target for the duration of the whole stream.
  /// </para>
  /// <para>
  /// Throws when the target was removed or is being removed. A streaming endpoint returns
  /// <see cref="IAsyncEnumerable{T}"/>, so it has no failed result to report and yielding nothing
  /// would read as an empty device list.
  /// </para>
  /// </summary>
  internal InFlightTracker.Lease BeginTrackedRequest() => Requests.AcquireOrThrow(nameof(ControlrApi));

  internal async Task<ApiResult> ExecuteApiCall(Func<Task> func, bool allowAutoRefresh = true)
  {
    using var lease = Requests.Acquire();

    if (!lease.Acquired)
    {
      return LogFailure(ApiResult.Fail(DisposedTargetReason, HttpStatusCode.InternalServerError));
    }

    try
    {
      await PrepareClientForRequest(allowAutoRefresh);
      await func.Invoke();
      return ApiResult.Ok();
    }
    catch (HttpRequestException ex)
    {
      if (allowAutoRefresh && await TryRefreshAfterUnauthorized(ex))
      {
        try
        {
          await PrepareClientForRequest(allowAutoRefresh: false);
          await func.Invoke();
          return ApiResult.Ok();
        }
        catch (HttpRequestException retryEx)
        {
          var retryResult = ApiResult.Fail(retryEx.Message, retryEx.StatusCode, retryEx.HttpRequestError);
          return LogFailure(retryResult, retryEx);
        }
      }

      var apiResult = ApiResult.Fail(ex.Message, ex.StatusCode, ex.HttpRequestError);
      return LogFailure(apiResult, ex);
    }
    catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
    {
      const string message = "The operation was canceled by the caller.";
      var apiResult = ApiResult.Fail(message, HttpStatusCode.RequestTimeout);
      return LogFailure(apiResult, ex);
    }
    catch (TaskCanceledException ex)
    {
      const string message = "The request timed out.";
      var apiResult = ApiResult.Fail(message, HttpStatusCode.RequestTimeout);
      return LogFailure(apiResult, ex);
    }
    catch (ObjectDisposedException ex)
    {
      // The target was removed or evicted while this client was still in use. Reporting that as a
      // server failure would send the caller looking at the wrong end of the connection.
      var apiResult = ApiResult.Fail(DisposedTargetReason, HttpStatusCode.InternalServerError);
      return LogFailure(apiResult, ex);
    }
    catch (Exception ex)
    {
      const string message = "The request to the server failed.";
      var apiResult = ApiResult.Fail(message, HttpStatusCode.InternalServerError);
      return LogFailure(apiResult, ex);
    }
  }

  internal async Task<ApiResult<T>> ExecuteApiCall<T>(Func<Task<T?>> func, bool allowAutoRefresh = true)
  {
    using var lease = Requests.Acquire();

    if (!lease.Acquired)
    {
      return LogFailure(ApiResult.Fail<T>(DisposedTargetReason, httpRequestError: HttpRequestError.Unknown));
    }

    try
    {
      await PrepareClientForRequest(allowAutoRefresh);
      var resultValue = await func.Invoke() ??
        throw new HttpRequestException("The server response was empty.");

      var validationErrors = DtoValidatorFactory.Validate(resultValue);
      if (validationErrors is not null)
      {
        if (_options.Value.DisableResponseDtoStrictness)
        {
          _logger.LogWarning("Response DTO validation failed but strictness is disabled: {Reason}", validationErrors);
          return ApiResult.Ok(resultValue);
        }

        var reason = $"DTO validation failed: {validationErrors}";
        var apiResult = ApiResult.Fail<T>(reason, HttpStatusCode.InternalServerError);
        return LogFailure(apiResult);
      }

      return ApiResult.Ok(resultValue);
    }
    catch (HttpRequestException ex)
    {
      if (allowAutoRefresh && await TryRefreshAfterUnauthorized(ex))
      {
        try
        {
          await PrepareClientForRequest(allowAutoRefresh: false);
          var retriedValue = await func.Invoke() ??
            throw new HttpRequestException("The server response was empty.");

          var validationErrors = DtoValidatorFactory.Validate(retriedValue);
          if (validationErrors is not null)
          {
            if (_options.Value.DisableResponseDtoStrictness)
            {
              _logger.LogWarning("Response DTO validation failed but strictness is disabled: {Reason}", validationErrors);
              return ApiResult.Ok(retriedValue);
            }

            var retryReason = $"DTO validation failed: {validationErrors}";
            var retryResult = ApiResult.Fail<T>(retryReason, HttpStatusCode.InternalServerError);
            return LogFailure(retryResult);
          }

          return ApiResult.Ok(retriedValue);
        }
        catch (HttpRequestException retryEx)
        {
          var retryResult = ApiResult.Fail<T>(retryEx.Message, retryEx.StatusCode, retryEx.HttpRequestError);
          return LogFailure(retryResult, retryEx);
        }
      }

      var apiResult = ApiResult.Fail<T>(ex.Message, ex.StatusCode, ex.HttpRequestError);
      return LogFailure(apiResult, ex);
    }
    catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
    {
      const string message = "The operation was canceled by the caller.";
      var apiResult = ApiResult.Fail<T>(message, HttpStatusCode.RequestTimeout);
      return LogFailure(apiResult, ex);
    }
    catch (TaskCanceledException ex)
    {
      const string message = "The request timed out.";
      var apiResult = ApiResult.Fail<T>(message, HttpStatusCode.RequestTimeout);
      return LogFailure(apiResult, ex);
    }
    catch (ObjectDisposedException ex)
    {
      // The target was removed or evicted while this client was still in use. Reporting that as a
      // server failure would send the caller looking at the wrong end of the connection.
      var apiResult = ApiResult.Fail<T>(DisposedTargetReason, httpRequestError: HttpRequestError.Unknown);
      return LogFailure(apiResult, ex);
    }
    catch (Exception ex)
    {
      const string message = "The request to the server failed.";
      var apiResult = ApiResult.Fail<T>(message, httpRequestError: HttpRequestError.Unknown);
      return LogFailure(apiResult, ex);
    }
  }

  private ApiResult LogFailure(ApiResult result, Exception? ex = null)
  {
    if (ex is null)
    {
      _logger.LogWarning("API request failed: {Reason}", result.Reason);
      return result;
    }

    _logger.LogError(ex, "API request failed: {Reason}", result.Reason);
    return result;
  }

  private ApiResult<T> LogFailure<T>(ApiResult<T> result, Exception? ex = null)
  {
    if (ex is null)
    {
      _logger.LogWarning("API request failed: {Reason}", result.Reason);
      return result;
    }

    _logger.LogError(ex, "API request failed: {Reason}", result.Reason);
    return result;
  }

  private async Task PrepareClientForRequest(bool allowAutoRefresh)
  {
    if (!allowAutoRefresh)
    {
      return;
    }

    try
    {
      await RefreshBearerTokenIfNeeded(forceRefresh: false);
    }
    catch (ObjectDisposedException)
    {
      // The target began being removed after this call announced itself, so the refresher was
      // refused. This call still holds the target, which means its stack is alive and stays alive
      // until the call returns. Sending with the token in hand is what the in-flight guarantee
      // promises; a resulting 401 takes the normal refresh-and-retry path.
    }
  }

  private async Task<bool> RefreshBearerTokenIfNeeded(bool forceRefresh)
  {
    var refreshResult = await _bearerTokenRefresher.RefreshIfNeeded(
      forceRefresh,
      _options.Value.BearerRefreshLeadTime);

    if (refreshResult == BearerTokenRefreshResult.Unauthorized)
    {
      _authState.ClearBearerTokens();
      throw new HttpRequestException(
        "The refresh token is no longer valid.",
        null,
        HttpStatusCode.Unauthorized);
    }

    if (refreshResult == BearerTokenRefreshResult.EndpointUnavailable)
    {
      _logger.LogWarning("Bearer token refresh endpoint is not available.");
      return false;
    }

    return refreshResult == BearerTokenRefreshResult.Refreshed;
  }

  private async Task<bool> TryRefreshAfterUnauthorized(HttpRequestException ex)
  {
    if (ex.StatusCode != HttpStatusCode.Unauthorized)
    {
      return false;
    }

    try
    {
      return await RefreshBearerTokenIfNeeded(forceRefresh: true);
    }
    catch (Exception refreshEx)
    {
      _logger.LogWarning(refreshEx, "Bearer token refresh failed after unauthorized response.");
      return false;
    }
  }
}