using System.Diagnostics.CodeAnalysis;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.DataRedaction;

namespace ControlR.ApiClient;

public class ControlrApiClientAuthState(string? personalAccessToken = null, string? serviceAccountApiKey = null)
{
  public const string AuthorizationHeader = "Authorization";

  private readonly Lock _stateLock = new();

  private ApiCredentials _credentials = new(personalAccessToken, serviceAccountApiKey, null);

  [ProtectedDataClassification]
  public string? BearerToken => GetCredentials().Bearer?.AccessToken;
  public DateTimeOffset? BearerTokenExpiresAt => GetCredentials().Bearer?.ExpiresAt;
  [ProtectedDataClassification]
  public string? PersonalAccessToken => GetCredentials().PersonalAccessToken;
  [ProtectedDataClassification]
  public string? RefreshToken => GetCredentials().Bearer?.RefreshToken;
  [ProtectedDataClassification]
  public string? ServiceAccountApiKey => GetCredentials().ServiceAccountApiKey;

  internal BearerCredential? Bearer => GetCredentials().Bearer;

  public void ClearPersonalAccessToken()
  {
    SetPersonalAccessToken(null);
  }

  public void ClearServiceAccountApiKey()
  {
    SetServiceAccountApiKey(null);
  }

  public AuthSnapshot GetSnapshot()
  {
    var credentials = GetCredentials();
    return new AuthSnapshot(
      credentials.PersonalAccessToken,
      credentials.Bearer?.AccessToken,
      credentials.Bearer?.ExpiresAt,
      credentials.Bearer?.RefreshToken,
      credentials.ServiceAccountApiKey);
  }

  public void SetPersonalAccessToken(string? personalAccessToken)
  {
    lock (_stateLock)
    {
      _credentials = _credentials with { PersonalAccessToken = personalAccessToken };
    }
  }

  public void SetServiceAccountApiKey(string? serviceAccountApiKey)
  {
    lock (_stateLock)
    {
      _credentials = _credentials with { ServiceAccountApiKey = serviceAccountApiKey };
    }
  }

  public override string ToString() => "[REDACTED]";

  public bool TryGetAuthHeader(
    [NotNullWhen(true)] out string? headerName,
    [NotNullWhen(true)] out string? headerValue)
  {
    return TryGetAuthHeader(out headerName, out headerValue, out _);
  }

  internal ApiCredentials GetCredentials() => Volatile.Read(ref _credentials);

  internal void Replace(ApiCredentials credentials)
  {
    lock (_stateLock)
    {
      _credentials = credentials;
    }
  }

  /// <summary>
  /// Picks the one credential to send. <paramref name="bearer"/> is set only when the bearer token is the
  /// credential chosen.
  /// </summary>
  internal bool TryGetAuthHeader(
    [NotNullWhen(true)] out string? headerName,
    [NotNullWhen(true)] out string? headerValue,
    out BearerCredential? bearer)
  {
    var credentials = GetCredentials();
    bearer = null;

    if (!string.IsNullOrWhiteSpace(credentials.PersonalAccessToken))
    {
      headerName = ControlrApiClientOptions.PersonalAccessTokenHeader;
      headerValue = credentials.PersonalAccessToken;
      return true;
    }

    if (credentials.Bearer is { } credential)
    {
      bearer = credential;
      headerName = AuthorizationHeader;
      headerValue = $"Bearer {credential.AccessToken}";
      return true;
    }

    // Last, so a service account key never silently displaces an already-configured credential.
    // The server's dynamic scheme selector likewise tests x-api-key after Authorization and
    // x-personal-token.
    if (!string.IsNullOrWhiteSpace(credentials.ServiceAccountApiKey))
    {
      headerName = ControlrApiClientOptions.ServiceAccountApiKeyHeader;
      headerValue = credentials.ServiceAccountApiKey;
      return true;
    }

    headerName = null;
    headerValue = null;
    return false;
  }

  /// <summary>
  /// Swaps the bearer credential only when <paramref name="expected"/> is still the current one, so a
  /// renewal that finishes after a sign-out or a newer sign-in cannot overwrite it.
  /// </summary>
  internal bool TryReplaceBearer(BearerCredential expected, BearerCredential? replacement)
  {
    lock (_stateLock)
    {
      if (!ReferenceEquals(_credentials.Bearer, expected))
      {
        return false;
      }

      _credentials = _credentials with { Bearer = replacement };
      return true;
    }
  }
}

internal sealed record ApiCredentials(
  string? PersonalAccessToken,
  string? ServiceAccountApiKey,
  BearerCredential? Bearer);

/// <summary>
/// An access token and the refresh token that renews it. Compared by reference, because the instance
/// identifies the exact token a request carried.
/// </summary>
internal sealed class BearerCredential(
  string accessToken,
  string refreshToken,
  DateTimeOffset expiresAt,
  TimeSpan? lifetime)
{
  public string AccessToken { get; } = accessToken;
  public DateTimeOffset ExpiresAt { get; } = expiresAt;
  public TimeSpan? Lifetime { get; } = lifetime;
  public string RefreshToken { get; } = refreshToken;
}
