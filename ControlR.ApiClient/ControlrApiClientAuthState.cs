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
  public TimeSpan? BearerTokenLifetime => GetSnapshot().BearerTokenLifetime;
  public bool CanRefreshBearerToken => CanRefresh(GetSnapshot());
  [ProtectedDataClassification]
  public string? PersonalAccessToken => GetSnapshot().PersonalAccessToken;
  [ProtectedDataClassification]
  public string? RefreshToken => GetSnapshot().RefreshToken;
  [ProtectedDataClassification]
  public string? ServiceAccountApiKey => GetSnapshot().ServiceAccountApiKey;

  /// <summary>
  /// Gets the token-generation counter. Every bearer mutation bumps it, so a waiter that queued
  /// behind a refresh can detect that the token was already renewed while it waited.
  /// </summary>
  internal long BearerStateVersion => Volatile.Read(ref _state).BearerStateVersion;

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
          RefreshToken = null,
          BearerTokenLifetime = null
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
    var lifetime = TimeSpan.FromSeconds(Math.Max(response.ExpiresInSeconds, 1));
    SetBearerTokens(
      response.AccessToken,
      response.RefreshToken,
      timeProvider.GetUtcNow().Add(lifetime),
      lifetime);
  }

  public void SetBearerTokens(
    string? bearerToken,
    string? refreshToken,
    DateTimeOffset? bearerTokenExpiresAt,
    TimeSpan? bearerTokenLifetime = null)
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
          BearerTokenExpiresAt = bearerTokenExpiresAt,
          BearerTokenLifetime = bearerTokenLifetime
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

    // Restates CanRefresh because nullable flow analysis cannot narrow record members through a
    // bool-returning helper, and BearerRefreshContext requires a non-null refresh token.
    if (string.IsNullOrWhiteSpace(snapshot.BearerToken) ||
        string.IsNullOrWhiteSpace(refreshToken) ||
        snapshot.BearerTokenExpiresAt is null)
    {
      return null;
    }

    if (!forceRefresh && GetTimeUntilRefresh(timeProvider, refreshWindow) > TimeSpan.Zero)
    {
      return null;
    }

    return new BearerRefreshContext(state.BearerStateVersion, refreshToken);
  }

  public bool TryGetAuthHeader(
    [NotNullWhen(true)] out string? headerName,
    [NotNullWhen(true)] out string? headerValue)
    => TryGetAuthHeaderFrom(GetSnapshot(), out headerName, out headerValue);

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
          BearerTokenExpiresAt = timeProvider.GetUtcNow().AddSeconds(Math.Max(response.ExpiresInSeconds, 1)),
          BearerTokenLifetime = TimeSpan.FromSeconds(Math.Max(response.ExpiresInSeconds, 1))
        }
      };
      return true;
    }
  }

  /// <summary>
  /// Caps the refresh lead time below half the lifetime the server actually granted. Without the
  /// cap, a server handing out tokens whose lifetime is at or below the lead time leaves every
  /// freshly renewed token already inside the refresh window, which makes the background loop and
  /// every proactive call refresh back-to-back forever: the Identity refresh endpoint is stateless,
  /// so a rotated-out refresh token stays valid and nothing counts a successful POST as a failure.
  /// The cap keeps proactive and background refreshes at least half a lifetime ahead of expiry.
  /// </summary>
  internal static TimeSpan GetBearerRefreshWindow(TimeSpan? bearerTokenLifetime, TimeSpan refreshWindow)
  {
    if (bearerTokenLifetime is not { } lifetime)
    {
      return refreshWindow;
    }

    var halfLifetime = lifetime / 2;
    return halfLifetime > TimeSpan.Zero && halfLifetime < refreshWindow ? halfLifetime : refreshWindow;
  }

  /// <summary>
  /// Gets how long to wait before the next proactive refresh. Zero means refresh now.
  /// </summary>
  /// <remarks>
  /// The background loop sleeps on this value and <see cref="TryCreateBearerRefreshContext"/> makes
  /// the same comparison, so a wake-up is never answered with "not stale yet" and then slept again.
  /// </remarks>
  internal TimeSpan GetTimeUntilRefresh(TimeProvider timeProvider, TimeSpan refreshLeadTime)
  {
    var snapshot = GetSnapshot();
    if (snapshot.BearerTokenExpiresAt is not { } expiresAt)
    {
      return TimeSpan.Zero;
    }

    var wait = expiresAt - timeProvider.GetUtcNow() - GetBearerRefreshWindow(snapshot.BearerTokenLifetime, refreshLeadTime);
    return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
  }

  internal bool TryGetAuthHeader(
    [NotNullWhen(true)] out string? headerName,
    [NotNullWhen(true)] out string? headerValue,
    out long bearerStateVersion)
  {
    var state = Volatile.Read(ref _state);
    bearerStateVersion = state.BearerStateVersion;
    return TryGetAuthHeaderFrom(state.Snapshot, out headerName, out headerValue);
  }

  private static bool CanRefresh(AuthSnapshot snapshot) =>
    !string.IsNullOrWhiteSpace(snapshot.BearerToken) &&
    !string.IsNullOrWhiteSpace(snapshot.RefreshToken) &&
    snapshot.BearerTokenExpiresAt is not null;

  private static bool TryGetAuthHeaderFrom(
    AuthSnapshot snapshot,
    [NotNullWhen(true)] out string? headerName,
    [NotNullWhen(true)] out string? headerValue)
  {
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

}