using System.Net.Http.Headers;

namespace ControlR.ApiClient;

public sealed class ControlrApiAuthHeaderHandler(ControlrApiClientAuthState authState) : DelegatingHandler
{
  internal static readonly HttpRequestOptionsKey<long> BearerStateVersionOption = new("ControlR.BearerStateVersionAtSend");

  private readonly ControlrApiClientAuthState _authState = authState;

  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    ApplyAuthHeader(request);
    return base.SendAsync(request, cancellationToken);
  }

  private void ApplyAuthHeader(HttpRequestMessage request)
  {
    var headers = request.Headers;
    // Scrub every credential header the client can emit before applying one, so the outgoing headers
    // are a function of the current auth state alone. A caller may have seeded a header on
    // HttpClient.DefaultRequestHeaders, which is merged into the request before this handler runs.
    headers.Remove(ControlrApiClientOptions.PersonalAccessTokenHeader);
    headers.Remove(ControlrApiClientOptions.ServiceAccountApiKeyHeader);
    headers.Remove(ControlrApiClientAuthState.AuthorizationHeader);
    headers.Authorization = null;

    if (!_authState.TryGetAuthHeader(out var headerName, out var headerValue, out var bearerStateVersion))
    {
      return;
    }

    if (headerName == ControlrApiClientAuthState.AuthorizationHeader)
    {
      headers.Authorization = AuthenticationHeaderValue.Parse(headerValue);
      request.Options.Set(BearerStateVersionOption, bearerStateVersion);
      return;
    }

    headers.Add(headerName, headerValue);
  }
}