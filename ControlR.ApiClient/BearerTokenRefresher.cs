using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public enum BearerTokenRefreshResult
{
  NoRefreshNeeded,
  Refreshed,
  Unauthorized,
  EndpointUnavailable
}

public interface IBearerTokenRefresher
{
  Task<BearerTokenRefreshResult> RefreshIfNeeded(
    bool forceRefresh,
    TimeSpan refreshWindow,
    Uri? baseUrl = null,
    CancellationToken cancellationToken = default);
}

public sealed class BearerTokenRefresher(
  ControlrApiClientAuthState authState,
  IHttpClientFactory httpClientFactory,
  TimeProvider timeProvider) : IBearerTokenRefresher
{
  private const string RefreshEndpoint = $"{HttpConstants.AuthEndpoint}/refresh";

  private readonly ControlrApiClientAuthState _authState = authState;
  private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<BearerTokenRefreshResult> RefreshIfNeeded(
    bool forceRefresh,
    TimeSpan refreshWindow,
    Uri? baseUrl = null,
    CancellationToken cancellationToken = default)
  {
    var auth = _authState;
    var refreshContext = auth.TryCreateBearerRefreshContext(_timeProvider, refreshWindow, forceRefresh);
    if (refreshContext is null)
    {
      return BearerTokenRefreshResult.NoRefreshNeeded;
    }

    await auth.BearerRefreshLock.WaitAsync(cancellationToken);
    try
    {
      refreshContext = auth.TryCreateBearerRefreshContext(_timeProvider, refreshWindow, forceRefresh);
      if (refreshContext is null)
      {
        return BearerTokenRefreshResult.NoRefreshNeeded;
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
      auth.BearerRefreshLock.Release();
    }
  }
}