using System.Net;
using ControlR.ApiClient.Interfaces.Agent;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.ApiClient.Internal;
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
  ILogger<ControlrApi> logger,
  IOptions<ControlrApiClientOptions> options) : IControlrApi
{
  internal const string DisposedTargetReason =
    "The HTTP client for this target was disposed. Obtain the client again from the factory.";

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
  /// Counts this client's calls so that evicting the target releases its HTTP stack once they finish.
  /// The factory assigns it while building the target.
  /// </summary>
  internal InFlightTracker Requests { get; set; } = new();
  internal V1Api V1 => _v1 ??= new(this);

  IControlrAgentApi IControlrApi.Agent => AgentApi;
  IControlrInternalApi IControlrApi.Internal => InternalApi;
  IControlrV1Api IControlrApi.V1 => V1;

  /// <summary>
  /// Announces a call that <see cref="ExecuteApiCall(Func{Task})"/> does not wrap, so a streamed response stays counted while its body is read.
  /// </summary>
  /// <remarks>
  /// Dispose the returned lease when the caller is done with the response. The JSON read from a streaming
  /// endpoint is buffered by <see cref="HttpClient"/> before it reaches the caller, so the lease is cleared
  /// when that read finishes rather than when the response starts. A caller that hands items out slowly
  /// holds the target for the duration of the whole stream.
  /// Throws when the target was removed or is being removed. A streaming endpoint returns
  /// <see cref="IAsyncEnumerable{T}"/>, so it has no failed result to report and yielding nothing
  /// would read as an empty device list.
  /// </remarks>
  internal InFlightTracker.Lease BeginTrackedRequest() => Requests.AcquireOrThrow(nameof(ControlrApi));

  internal async Task<ApiResult> ExecuteApiCall(Func<Task> func)
  {
    using var lease = Requests.Acquire();

    if (!lease.Acquired)
    {
      return LogFailure(ApiResult.Fail(DisposedTargetReason, httpRequestError: HttpRequestError.Unknown));
    }

    try
    {
      await func.Invoke();
      return ApiResult.Ok();
    }
    catch (HttpRequestException ex)
    {
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
      var apiResult = ApiResult.Fail(DisposedTargetReason, httpRequestError: HttpRequestError.Unknown);
      return LogFailure(apiResult, ex);
    }
    catch (Exception ex)
    {
      const string message = "The request to the server failed.";
      var apiResult = ApiResult.Fail(message, HttpStatusCode.InternalServerError);
      return LogFailure(apiResult, ex);
    }
  }

  internal async Task<ApiResult<T>> ExecuteApiCall<T>(Func<Task<T?>> func)
  {
    using var lease = Requests.Acquire();

    if (!lease.Acquired)
    {
      return LogFailure(ApiResult.Fail<T>(DisposedTargetReason, httpRequestError: HttpRequestError.Unknown));
    }

    try
    {
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
}