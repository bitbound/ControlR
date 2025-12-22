namespace ControlR.Libraries.WebSocketRelay.Client;

public static class RelayUriBuilder
{
  public static Uri Build(
    Uri baseUri,
    string path,
    Guid sessionId,
    string accessToken,
    RelayRole role,
    uint timeoutSeconds)
  {
    if (baseUri.Scheme != Uri.UriSchemeWs && baseUri.Scheme != Uri.UriSchemeWss)
    {
      throw new ArgumentException("Base URI must use ws:// or wss:// scheme.", nameof(baseUri));
    }

    var uriBuilder = new UriBuilder(baseUri)
    {
      Scheme = baseUri.Scheme,
      Host = baseUri.Host,
      Port = baseUri.Port,
      Path = path,
      Query = $"?sessionId={sessionId}&" + 
        $"accessToken={accessToken}&" + 
        $"timeout={timeoutSeconds}&" + 
        $"role={role.ToString().ToLowerInvariant()}"
    };

    return uriBuilder.Uri;
  }
}