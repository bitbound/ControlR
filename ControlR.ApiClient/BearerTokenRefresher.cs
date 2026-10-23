using System.Net;
using System.Net.Http.Json;
using ControlR.ApiClient.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

public enum BearerTokenRefreshResult
{
  NoRefreshNeeded,
  Refreshed,
  Unauthorized,
  EndpointUnavailable
}

/// <summary>
/// Refreshes interactive bearer tokens for a ControlR API client when they are near expiration.
/// </summary>
public interface IBearerTokenRefresher
{
  /// <summary>
  /// Refreshes the current bearer token when required by the refresh window or when forced.
  /// </summary>
  /// <param name="forceRefresh">Indicates whether to refresh even when the token is not yet near expiration.</param>
  /// <param name="refreshWindow">The lead time used to decide when a token should be refreshed.</param>
  /// <param name="baseUrl">An optional base URL override for the refresh request.</param>
  /// <param name="cancellationToken">Cancels the refresh operation.</param>
  /// <returns>The outcome of the refresh attempt.</returns>
  Task<BearerTokenRefreshResult> RefreshIfNeeded(
    bool forceRefresh,
    TimeSpan refreshWindow,
    Uri? baseUrl = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Refreshes like the four-argument overload, but a forced call may dedupe against
  /// <paramref name="dedupeAgainstBearerStateVersion"/>: when the generation no longer matches the
  /// live state, the token was renewed after the caller's request went out, and the call rides the
  /// renewal instead of posting another refresh.
  /// </summary>
  /// <param name="forceRefresh">Indicates whether to refresh even when the token is not yet near expiration.</param>
  /// <param name="refreshWindow">The lead time used to decide when a token should be refreshed.</param>
  /// <param name="baseUrl">An optional base URL override for the refresh request.</param>
  /// <param name="cancellationToken">Cancels the refresh operation.</param>
  /// <param name="dedupeAgainstBearerStateVersion">
  /// The token-generation counter the failed request carried. Null when nothing trustworthy can be
  /// deduped against: either no caller request stands behind the refresh, or the challenge carries
  /// no applied-generation stamp.
  /// </param>
  /// <returns>The outcome of the refresh attempt.</returns>
  /// <remarks>
  /// The default implementation ignores the deduplication and calls the four-argument overload, so
  /// implementations written before this overload existed keep working. They just pay one refresh
  /// POST per concurrent 401 waiter.
  /// </remarks>
  Task<BearerTokenRefreshResult> RefreshIfNeeded(
    bool forceRefresh,
    TimeSpan refreshWindow,
    Uri? baseUrl,
    CancellationToken cancellationToken,
    long? dedupeAgainstBearerStateVersion)
  {
    return RefreshIfNeeded(forceRefresh, refreshWindow, baseUrl, cancellationToken);
  }
}

public sealed class BearerTokenRefresher(
  ControlrApiClientAuthState authState,
  IHttpClientFactory httpClientFactory,
  TimeProvider timeProvider) : IBearerTokenRefresher
{
  private const string RefreshEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/refresh";

  private readonly ControlrApiClientAuthState _authState = authState;
  private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
  private readonly TimeProvider _timeProvider = timeProvider;

  /// <summary>
  /// Counts refreshes so that a tracked target is not released while one is waiting or in flight.
  /// The factory assigns the target's tracker here.
  /// </summary>
  internal InFlightTracker Requests { get; set; } = new();

  public Task<BearerTokenRefreshResult> RefreshIfNeeded(
    bool forceRefresh,
    TimeSpan refreshWindow,
    Uri? baseUrl = null,
    CancellationToken cancellationToken = default)
    => RefreshIfNeeded(forceRefresh, refreshWindow, baseUrl, cancellationToken, dedupeAgainstBearerStateVersion: null);

  public async Task<BearerTokenRefreshResult> RefreshIfNeeded(
    bool forceRefresh,
    TimeSpan refreshWindow,
    Uri? baseUrl,
    CancellationToken cancellationToken,
    long? dedupeAgainstBearerStateVersion)
  {
    var auth = _authState;
    var refreshContext = auth.TryCreateBearerRefreshContext(_timeProvider, refreshWindow, forceRefresh);
    if (refreshContext is null)
    {
      return BearerTokenRefreshResult.NoRefreshNeeded;
    }

    // Queuing for the refresh lock has to be tracked, not just the request that follows it. A
    // factory teardown disposes that lock together with the target, and a waiter that is already
    // queued when the dispose lands is never resumed. Reporting "nothing to refresh" instead lets
    // the caller continue to its own call, which reports the removed target on its own.
    using var lease = Requests.AcquireOrThrow(nameof(BearerTokenRefresher));

    await auth.BearerRefreshLock.WaitAsync(cancellationToken);
    try
    {
      refreshContext = auth.TryCreateBearerRefreshContext(_timeProvider, refreshWindow, forceRefresh);
      if (refreshContext is null)
      {
        return BearerTokenRefreshResult.NoRefreshNeeded;
      }

      // A caller whose challenge predates a renewal rides that renewal. The forced path skips the
      // freshness check, so without this comparison every concurrent 401 in a burst costs its own
      // round trip to the stateless refresh endpoint. The version bumps on every bearer mutation,
      // so a moved version proves the token already changed underneath the caller.
      if (dedupeAgainstBearerStateVersion is { } observedVersion &&
          refreshContext.ExpectedBearerStateVersion != observedVersion)
      {
        return BearerTokenRefreshResult.Refreshed;
      }

      var refreshClient = _httpClientFactory.CreateClient(ControlrApiClientNames.UnauthenticatedClient);
      using var response = baseUrl is null
        ? await refreshClient.PostAsJsonAsync(
          RefreshEndpoint,
          new RefreshTokenRequestDto(refreshContext.RefreshToken),
          cancellationToken)
        : await refreshClient.PostAsJsonAsync(
          new Uri(baseUrl, RefreshEndpoint),
          new RefreshTokenRequestDto(refreshContext.RefreshToken),
          cancellationToken);

      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        return BearerTokenRefreshResult.Unauthorized;
      }

      if (response.StatusCode == HttpStatusCode.NotFound)
      {
        return BearerTokenRefreshResult.EndpointUnavailable;
      }

      await response.EnsureSuccessStatusCodeWithDetails();

      var refreshedTokens = await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(cancellationToken) ??
        throw new HttpRequestException("The refresh response was empty.");

      if (!auth.TrySetBearerTokenResponse(
        refreshedTokens,
        refreshContext.ExpectedBearerStateVersion,
        _timeProvider))
      {
        return BearerTokenRefreshResult.NoRefreshNeeded;
      }

      return BearerTokenRefreshResult.Refreshed;
    }
    finally
    {
      try
      {
        auth.BearerRefreshLock.Release();
      }
      catch (ObjectDisposedException)
      {
        // Backstop. The lease taken above is what normally keeps a tracked target's teardown from
        // disposing this semaphore while it is held. This covers any other owner that disposes the
        // auth state mid-flight. The refresh result is already applied to the auth state, which is
        // being discarded anyway, so swallowing keeps a successful refresh from crashing in the
        // finally block.
      }
    }
  }
}