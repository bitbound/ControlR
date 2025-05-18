namespace ControlR.Streamer.Options;

public class StartupOptions
{
  private Uri? _serverOrigin;
  private Uri? _webSocketUri;

  public bool NotifyUser { get; set; }

  public Uri ServerOrigin
  {
    get => _serverOrigin ?? throw new InvalidOperationException("ServerOrigin hasn't been set yet.");
    set => _serverOrigin = value;
  }

  public Guid SessionId { get; set; }

  public string? ViewerName { get; set; }

  public Uri WebSocketUri
  {
    get => _webSocketUri ?? throw new InvalidOperationException("WebSocketUri hasn't been set yet.");
    set => _webSocketUri = value;
  }
}
