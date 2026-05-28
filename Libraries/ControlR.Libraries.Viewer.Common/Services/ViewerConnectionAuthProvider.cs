using ControlR.ApiClient;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.Viewer.Common.Services;

/// <summary>
/// Supplies authentication details for viewer SignalR and WebSocket connections.
/// </summary>
public interface IViewerConnectionAuthProvider
{
  /// <summary>
  /// Configures SignalR connection options with the authentication mechanism selected for the viewer.
  /// </summary>
  /// <param name="options">The SignalR connection options to configure.</param>
  void ConfigureSignalr(HttpConnectionOptions options);

  /// <summary>
  /// Builds the HTTP headers required for authenticated WebSocket relay connections.
  /// </summary>
  /// <param name="cancellationToken">Cancels header generation when bearer-token acquisition is in progress.</param>
  /// <returns>A read-only dictionary of headers to attach to the WebSocket request.</returns>
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

    options.AccessTokenProvider = () => _authSession.GetBearerToken();
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

    var accessToken = await _authSession.GetBearerToken(cancellationToken);
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