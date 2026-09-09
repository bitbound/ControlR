using System.Diagnostics.CodeAnalysis;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.DataRedaction;

namespace ControlR.ApiClient;

public class ControlrApiClientAuthState(string? personalAccessToken = null, string? serviceAccountApiKey = null)
{
  public const string AuthorizationHeader = "Authorization";

  private readonly Lock _stateLock = new();

  private AuthState _state = new(
    BearerStateVersion: 0,
    Snapshot: new AuthSnapshot(personalAccessToken, null, null, null, serviceAccountApiKey));

  public SemaphoreSlim BearerRefreshLock { get; } = new(1, 1);
  [ProtectedDataClassification]
  public string? BearerToken => GetSnapshot().BearerToken;
  public DateTimeOffset? BearerTokenExpiresAt => GetSnapshot().BearerTokenExpiresAt;
  public bool CanRefreshBearerToken => CanRefresh(GetSnapshot());
  [ProtectedDataClassification]
  public string? PersonalAccessToken => GetSnapshot().PersonalAccessToken;
  [ProtectedDataClassification]
  public string? RefreshToken => GetSnapshot().RefreshToken;
  [ProtectedDataClassification]
  public string? ServiceAccountApiKey => GetSnapshot().ServiceAccountApiKey;

  public void ClearBearerTokens()
  {
    lock (_stateLock)
    {
      var state = _state;
      _state = state with
      {
        BearerStateVersion = state.BearerStateVersion + 1,
        Snapshot = state.Snapshot with
        {
          BearerToken = null,
          BearerTokenExpiresAt = null,
          RefreshToken = null
        }
      };
    }
  }

  public void ClearPersonalAccessToken()
  {
    SetPersonalAccessToken(null);
  }

  public void ClearServiceAccountApiKey()
  {
    SetServiceAccountApiKey(null);
  }

  public AuthSnapshot GetSnapshot() => Volatile.Read(ref _state).Snapshot;

  public void SetBearerTokenResponse(AccessTokenResponseDto response, TimeProvider timeProvider)
  {
    SetBearerTokens(
      response.AccessToken,
      response.RefreshToken,
      timeProvider.GetUtcNow().AddSeconds(Math.Max(response.ExpiresInSeconds, 1)));
  }

  public void SetBearerTokens(string? bearerToken, string? refreshToken, DateTimeOffset? bearerTokenExpiresAt)
  {
    lock (_stateLock)
    {
      var state = _state;
      _state = state with
      {
        BearerStateVersion = state.BearerStateVersion + 1,
        Snapshot = state.Snapshot with
        {
          BearerToken = bearerToken,
          RefreshToken = refreshToken,
          BearerTokenExpiresAt = bearerTokenExpiresAt
        }
      };
    }
  }

  public void SetPersonalAccessToken(string? personalAccessToken)
  {
    lock (_stateLock)
    {
      var state = _state;
      _state = state with
      {
        Snapshot = state.Snapshot with
        {
          PersonalAccessToken = personalAccessToken
        }
      };
    }
  }

  public void SetServiceAccountApiKey(string? serviceAccountApiKey)
  {
    lock (_stateLock)
    {
      var state = _state;
      _state = state with
      {
        Snapshot = state.Snapshot with
        {
          ServiceAccountApiKey = serviceAccountApiKey
        }
      };
    }
  }

  public override string ToString() => "[REDACTED]";

  public BearerRefreshContext? TryCreateBearerRefreshContext(
    TimeProvider timeProvider,
    TimeSpan refreshWindow,
    bool forceRefresh)
  {
    var state = Volatile.Read(ref _state);
    var snapshot = state.Snapshot;
    var refreshToken = snapshot.RefreshToken;
    var bearerTokenExpiresAt = snapshot.BearerTokenExpiresAt;

    // Restates CanRefresh because nullable flow analysis cannot narrow record members through a
    // bool-returning helper, and BearerRefreshContext requires a non-null refresh token.
    if (string.IsNullOrWhiteSpace(snapshot.BearerToken) ||
        string.IsNullOrWhiteSpace(refreshToken) ||
        bearerTokenExpiresAt is null)
    {
      return null;
    }

    if (!forceRefresh && bearerTokenExpiresAt > timeProvider.GetUtcNow() + refreshWindow)
    {
      return null;
    }

    return new BearerRefreshContext(state.BearerStateVersion, refreshToken);
  }

  public bool TryGetAuthHeader(
    [NotNullWhen(true)] out string? headerName,
    [NotNullWhen(true)] out string? headerValue)
  {
    var snapshot = GetSnapshot();
    if (!string.IsNullOrWhiteSpace(snapshot.PersonalAccessToken))
    {
      headerName = ControlrApiClientOptions.PersonalAccessTokenHeader;
      headerValue = snapshot.PersonalAccessToken;
      return true;
    }

    if (!string.IsNullOrWhiteSpace(snapshot.BearerToken))
    {
      headerName = AuthorizationHeader;
      headerValue = $"Bearer {snapshot.BearerToken}";
      return true;
    }

    // Last, so a service account key never silently displaces an already-configured credential.
    // The server's dynamic scheme selector likewise tests x-api-key after Authorization and
    // x-personal-token.
    if (!string.IsNullOrWhiteSpace(snapshot.ServiceAccountApiKey))
    {
      headerName = ControlrApiClientOptions.ServiceAccountApiKeyHeader;
      headerValue = snapshot.ServiceAccountApiKey;
      return true;
    }

    headerName = null;
    headerValue = null;
    return false;
  }

  public bool TrySetBearerTokenResponse(
    AccessTokenResponseDto response,
    long expectedBearerStateVersion,
    TimeProvider timeProvider)
  {
    lock (_stateLock)
    {
      var state = _state;
      if (state.BearerStateVersion != expectedBearerStateVersion)
      {
        return false;
      }

      _state = state with
      {
        BearerStateVersion = state.BearerStateVersion + 1,
        Snapshot = state.Snapshot with
        {
          BearerToken = response.AccessToken,
          RefreshToken = response.RefreshToken,
          BearerTokenExpiresAt = timeProvider.GetUtcNow().AddSeconds(Math.Max(response.ExpiresInSeconds, 1))
        }
      };
      return true;
    }
  }

  private static bool CanRefresh(AuthSnapshot snapshot) =>
    !string.IsNullOrWhiteSpace(snapshot.BearerToken) &&
    !string.IsNullOrWhiteSpace(snapshot.RefreshToken) &&
    snapshot.BearerTokenExpiresAt is not null;

}