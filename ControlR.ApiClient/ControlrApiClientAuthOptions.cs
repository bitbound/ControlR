using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.DataRedaction;

namespace ControlR.ApiClient;

public class ControlrApiClientAuthOptions
{
  public const string AuthorizationHeader = "Authorization";

  public SemaphoreSlim BearerRefreshLock { get; } = new(1, 1);
  
  [ProtectedDataClassification]
  public string? BearerToken { get; set; }
  public DateTimeOffset? BearerTokenExpiresAt { get; set; }

  public bool CanRefreshBearerToken =>
    !string.IsNullOrWhiteSpace(BearerToken) &&
    !string.IsNullOrWhiteSpace(RefreshToken) &&
    BearerTokenExpiresAt is not null;

  public bool HasAuthConfigured =>
    !string.IsNullOrWhiteSpace(PersonalAccessToken) ||
    !string.IsNullOrWhiteSpace(BearerToken);

  [ProtectedDataClassification]
  public string? PersonalAccessToken { get; set; }

  [ProtectedDataClassification]
  public string? RefreshToken { get; set; }

  public void ClearBearerTokens()
  {
    BearerToken = null;
    BearerTokenExpiresAt = null;
    RefreshToken = null;
  }

  public void SetBearerTokenResponse(AccessTokenResponseDto response, TimeProvider timeProvider)
  {
    BearerToken = response.AccessToken;
    BearerTokenExpiresAt = timeProvider.GetUtcNow().AddSeconds(response.ExpiresIn);
    RefreshToken = response.RefreshToken;
  }

  public bool ShouldRefreshBearerToken(TimeProvider timeProvider, TimeSpan refreshWindow)
  {
    if (!CanRefreshBearerToken || BearerTokenExpiresAt is null)
    {
      return false;
    }

    return BearerTokenExpiresAt <= timeProvider.GetUtcNow() + refreshWindow;
  }

  public override string ToString() => "[REDACTED]";

  public bool TryGetAuthHeader(
    [NotNullWhen(true)] out string? headerName,
    [NotNullWhen(true)] out string? headerValue)
  {
    if (!string.IsNullOrWhiteSpace(PersonalAccessToken))
    {
      headerName = ControlrApiClientOptions.PersonalAccessTokenHeader;
      headerValue = PersonalAccessToken;
      return true;
    }

    if (!string.IsNullOrWhiteSpace(BearerToken))
    {
      headerName = AuthorizationHeader;
      headerValue = $"Bearer {BearerToken}";
      return true;
    }

    headerName = null;
    headerValue = null;
    return false;
  }
}