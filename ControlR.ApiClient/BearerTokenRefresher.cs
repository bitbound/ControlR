using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public enum BearerTokenRefreshResult
{
  NoRefreshNeeded,
  Refreshed,
  Unauthorized
}

public interface IBearerTokenRefresher
{
  Task<BearerTokenRefreshResult> RefreshIfNeeded(
    bool forceRefresh,
    TimeSpan refreshWindow,
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
    CancellationToken cancellationToken = default)
  {
    var auth = _authState;
    if (!auth.CanRefreshBearerToken)
    {
      return BearerTokenRefreshResult.NoRefreshNeeded;
    }

    if (!forceRefresh && !auth.ShouldRefreshBearerToken(_timeProvider, refreshWindow))
    {
      return BearerTokenRefreshResult.NoRefreshNeeded;
    }

    await auth.BearerRefreshLock.WaitAsync(cancellationToken);
    try
    {
      if (!auth.CanRefreshBearerToken)
      {
        return BearerTokenRefreshResult.NoRefreshNeeded;
      }

      if (!forceRefresh && !auth.ShouldRefreshBearerToken(_timeProvider, refreshWindow))
      {
        return BearerTokenRefreshResult.NoRefreshNeeded;
      }

      var refreshToken = auth.RefreshToken;
      if (string.IsNullOrWhiteSpace(refreshToken))
      {
        return BearerTokenRefreshResult.NoRefreshNeeded;
      }

      var refreshClient = _httpClientFactory.CreateClient(ControlrApiClientNames.UnauthenticatedClient);
      using var response = await refreshClient.PostAsJsonAsync(
        RefreshEndpoint,
        new RefreshTokenRequestDto(refreshToken),
        cancellationToken);

      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        return BearerTokenRefreshResult.Unauthorized;
      }

      await response.EnsureSuccessStatusCodeWithDetails();

      var refreshedTokens = await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(cancellationToken) ??
        throw new HttpRequestException("The refresh response was empty.");

      auth.SetBearerTokenResponse(refreshedTokens, _timeProvider);
      return BearerTokenRefreshResult.Refreshed;
    }
    finally
    {
      auth.BearerRefreshLock.Release();
    }
  }
}