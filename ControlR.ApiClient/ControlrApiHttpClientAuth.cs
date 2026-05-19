using System.Net.Http.Headers;

namespace ControlR.ApiClient;

internal static class ControlrApiHttpClientAuth
{
  public static void ApplyAuthHeader(HttpClient client, ControlrApiClientOptions options)
  {
    client.DefaultRequestHeaders.Remove(ControlrApiClientOptions.PersonalAccessTokenHeader);
    client.DefaultRequestHeaders.Remove(ControlrApiClientAuthOptions.AuthorizationHeader);
    client.DefaultRequestHeaders.Authorization = null;

    if (!options.Auth.TryGetAuthHeader(out var headerName, out var headerValue))
    {
      return;
    }

    if (headerName == ControlrApiClientAuthOptions.AuthorizationHeader)
    {
      client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(headerValue);
      return;
    }

    client.DefaultRequestHeaders.Add(headerName, headerValue);
  }
}