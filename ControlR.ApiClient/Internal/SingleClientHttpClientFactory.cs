namespace ControlR.ApiClient.Internal;

/// <summary>
/// An <see cref="IHttpClientFactory"/> that always returns one pre-built client. The unauthenticated
/// endpoints (token refresh, interactive sign-in) already pass absolute URIs, so a single shared
/// client serves every target.
/// </summary>
internal sealed class SingleClientHttpClientFactory(HttpClient client) : IHttpClientFactory
{
  public HttpClient CreateClient(string name) => client;
}
