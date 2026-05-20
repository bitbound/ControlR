using System.Net.Http.Headers;

namespace ControlR.ApiClient;

internal static class ControlrApiHttpClientAuth
{
  public static void ApplyAuthHeader(HttpClient client, ControlrApiClientAuthState authState)
  {
    client.DefaultRequestHeaders.Remove(ControlrApiClientOptions.PersonalAccessTokenHeader);
    client.DefaultRequestHeaders.Remove(ControlrApiClientAuthState.AuthorizationHeader);
    client.DefaultRequestHeaders.Authorization = null;

    if (!authState.TryGetAuthHeader(out var headerName, out var headerValue))
    {
      return;
    }

    if (headerName == ControlrApiClientAuthState.AuthorizationHeader)
    {
      client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(headerValue);
      return;
    }

    client.DefaultRequestHeaders.Add(headerName, headerValue);
  }
}