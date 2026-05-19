using ControlR.ApiClient;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Options;

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
    if (!string.IsNullOrWhiteSpace(_options.Auth.PersonalAccessToken))
    {
      options.Headers[ControlrApiClientOptions.PersonalAccessTokenHeader] = _options.Auth.PersonalAccessToken;
      return;
    }

    options.AccessTokenProvider = _authSession.GetAccessToken;
  }

  public async Task<IReadOnlyDictionary<string, string>> GetWebSocketHeaders(CancellationToken cancellationToken = default)
  {
    if (!string.IsNullOrWhiteSpace(_options.Auth.PersonalAccessToken))
    {
      return new Dictionary<string, string>
      {
        [ControlrApiClientOptions.PersonalAccessTokenHeader] = _options.Auth.PersonalAccessToken
      };
    }

    var accessToken = await _authSession.GetAccessToken();
    if (string.IsNullOrWhiteSpace(accessToken))
    {
      return new Dictionary<string, string>();
    }

    return new Dictionary<string, string>
    {
      [ControlrApiClientAuthOptions.AuthorizationHeader] = $"Bearer {accessToken}"
    };
  }
}