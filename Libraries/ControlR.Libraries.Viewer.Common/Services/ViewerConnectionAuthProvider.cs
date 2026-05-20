using ControlR.ApiClient;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Options;
using ControlR.Libraries.Viewer.Common.Options;

namespace ControlR.Libraries.Viewer.Common.Services;

public interface IViewerConnectionAuthProvider
{
  void ConfigureSignalr(HttpConnectionOptions options);
  Task<IReadOnlyDictionary<string, string>> GetWebSocketHeaders(CancellationToken cancellationToken = default);
}

public class ViewerConnectionAuthProvider(
  IControlrAuthSession authSession,
  IOptions<ControlrViewerOptions> options) : IViewerConnectionAuthProvider
{
  private readonly IControlrAuthSession _authSession = authSession;
  private readonly ControlrViewerOptions _options = options.Value;

  public void ConfigureSignalr(HttpConnectionOptions options)
  {
    if (_options.AuthenticationMethod == ViewerAuthenticationMethod.PersonalAccessToken)
    {
      if (!string.IsNullOrWhiteSpace(_options.PersonalAccessToken))
      {
        options.Headers[ControlrApiClientOptions.PersonalAccessTokenHeader] = _options.PersonalAccessToken;
      }

      return;
    }

    options.AccessTokenProvider = () => _authSession.GetAccessToken();
  }

  public async Task<IReadOnlyDictionary<string, string>> GetWebSocketHeaders(CancellationToken cancellationToken = default)
  {
    if (_options.AuthenticationMethod == ViewerAuthenticationMethod.PersonalAccessToken)
    {
      if (string.IsNullOrWhiteSpace(_options.PersonalAccessToken))
      {
        return new Dictionary<string, string>();
      }

      return new Dictionary<string, string>
      {
        [ControlrApiClientOptions.PersonalAccessTokenHeader] = _options.PersonalAccessToken
      };
    }

    var accessToken = await _authSession.GetAccessToken(cancellationToken);
    if (string.IsNullOrWhiteSpace(accessToken))
    {
      return new Dictionary<string, string>();
    }

    return new Dictionary<string, string>
    {
      [ControlrApiClientAuthState.AuthorizationHeader] = $"Bearer {accessToken}"
    };
  }
}