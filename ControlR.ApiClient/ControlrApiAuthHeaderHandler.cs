using System.Net;
using System.Net.Http.Headers;

namespace ControlR.ApiClient;

public sealed class ControlrApiAuthHeaderHandler(
  ControlrApiClientAuthState authState,
  ControlrAuthSession? authSession = null) : DelegatingHandler
{
  private readonly ControlrAuthSession? _authSession = authSession;
  private readonly ControlrApiClientAuthState _authState = authState;

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    if (_authSession is not null)
    {
      await _authSession.GetFreshBearer(cancellationToken);
    }

    var sentBearer = ApplyAuthHeader(request.Headers);
    var response = await base.SendAsync(request, cancellationToken);

    if (response.StatusCode == HttpStatusCode.Unauthorized && sentBearer is not null && _authSession is not null)
    {
      // Renewing here, keyed on the exact token that was rejected, lets a burst of 401s share one
      // refresh. ControlrApi then retries the call once on the renewed token.
      await _authSession.RenewIfCurrent(sentBearer, cancellationToken);
    }

    return response;
  }

  private BearerCredential? ApplyAuthHeader(HttpRequestHeaders headers)
  {
    // Scrub every credential header the client can emit before applying one, so the outgoing headers
    // are a function of the current auth state alone. A caller may have seeded a header on
    // HttpClient.DefaultRequestHeaders, which is merged into the request before this handler runs.
    headers.Remove(ControlrApiClientOptions.PersonalAccessTokenHeader);
    headers.Remove(ControlrApiClientOptions.ServiceAccountApiKeyHeader);
    headers.Authorization = null;

    if (!_authState.TryGetAuthHeader(out var headerName, out var headerValue, out var bearer))
    {
      return null;
    }

    if (bearer is not null)
    {
      headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer.AccessToken);
      return bearer;
    }

    headers.Add(headerName, headerValue);
    return null;
  }
}
