using System.Net;
using System.Net.Http.Headers;
using ControlR.ApiClient.Internal;
using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.ApiClient;

public sealed class ControlrApiAuthHeaderHandler(
  ControlrApiClientAuthState authState,
  ControlrAuthSession? authSession = null) : DelegatingHandler
{
  /// <summary>
  /// The endpoints that mint or rotate a credential from something other than the current bearer token.
  /// </summary>
  private static readonly string[] _credentialEndpoints =
  [
    $"{HttpConstants.Internal.AuthEndpoint}/login",
    $"{HttpConstants.Internal.AuthEndpoint}/interactive-login",
    $"{HttpConstants.Internal.AuthEndpoint}/refresh"
  ];

  private readonly ControlrAuthSession? _authSession = authSession;
  private readonly ControlrApiClientAuthState _authState = authState;

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var challengesBearer = !IsCredentialRequest(request);

    if (challengesBearer && _authSession is not null)
    {
      await _authSession.GetFreshBearer(cancellationToken);
    }

    var sentBearer = ApplyAuthHeader(request.Headers);

    if (challengesBearer && BearerAttempt.Current is { } attempt)
    {
      attempt.SentBearer = sentBearer;
    }

    var response = await base.SendAsync(request, cancellationToken);

    if (challengesBearer &&
        response.StatusCode == HttpStatusCode.Unauthorized &&
        sentBearer is not null &&
        _authSession is not null)
    {
      // Renewing here, keyed on the exact token that was rejected, lets a burst of 401s share one
      // refresh. ControlrApi then retries the call once on the renewed token.
      await _authSession.RenewIfCurrent(sentBearer, cancellationToken);
    }

    return response;
  }

  /// <summary>
  /// Whether the request is an anonymous credential-minting call. A 401 from one means the posted
  /// credentials were wrong, never that the bearer this client holds is stale. Renewing for one would
  /// replay the login and spend a second anonymous rate-limit permit per user attempt, and a rejected
  /// refresh token would expire a live session while the user was still typing a password.
  /// </summary>
  private static bool IsCredentialRequest(HttpRequestMessage request)
  {
    var path = request.RequestUri?.AbsolutePath;
    return path is not null && _credentialEndpoints.Any(endpoint => path.EndsWith(endpoint, StringComparison.Ordinal));
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
