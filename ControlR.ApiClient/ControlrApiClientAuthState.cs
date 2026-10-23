using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.DataRedaction;

namespace ControlR.ApiClient;

public class ControlrApiClientAuthState(string? personalAccessToken = null, string? serviceAccountApiKey = null)
{
  private readonly Lock _stateLock = new();

  private ApiCredentials _credentials = new(personalAccessToken, serviceAccountApiKey);

  [ProtectedDataClassification]
  public string? PersonalAccessToken => GetCredentials().PersonalAccessToken;
  [ProtectedDataClassification]
  public string? ServiceAccountApiKey => GetCredentials().ServiceAccountApiKey;

  public void ClearPersonalAccessToken()
  {
    SetPersonalAccessToken(null);
  }

  public void ClearServiceAccountApiKey()
  {
    SetServiceAccountApiKey(null);
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
    var credentials = GetCredentials();

    if (!string.IsNullOrWhiteSpace(credentials.PersonalAccessToken))
    {
      headerName = ControlrApiClientOptions.PersonalAccessTokenHeader;
      headerValue = credentials.PersonalAccessToken;
      return true;
    }

    // Last, so a service account key never silently displaces an already-configured credential.
    // The server's dynamic scheme selector likewise tests x-api-key after x-personal-token.
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

  internal ApiCredentials GetCredentials() => Volatile.Read(ref _credentials);
}

internal sealed record ApiCredentials(string? PersonalAccessToken, string? ServiceAccountApiKey);
