using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.DataRedaction;
using Microsoft.Extensions.Compliance.Classification;

namespace ControlR.ApiClient;

public class ControlrApiClientAuthOptions
{
  public const string AuthorizationHeader = "Authorization";

  [ProtectedDataClassification]
  public string? BearerToken { get; set; }
  public bool HasAuthConfigured =>
    !string.IsNullOrWhiteSpace(PersonalAccessToken) ||
    !string.IsNullOrWhiteSpace(BearerToken);
  [ProtectedDataClassification]
  public string? PersonalAccessToken { get; set; }

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