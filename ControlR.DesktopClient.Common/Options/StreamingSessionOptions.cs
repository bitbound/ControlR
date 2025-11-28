using ControlR.Libraries.Shared.Enums;

namespace ControlR.DesktopClient.Common.Options;

public class StreamingSessionOptions
{
  private Uri? _serverOrigin;
  private Uri? _webSocketUri;

  public CaptureEncoderType EncoderType { get; set; } = CaptureEncoderType.Jpeg;
  public bool NotifyUser { get; set; }
  public int Quality { get; set; } = 75;
  public bool RequireConsent { get; set; }
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
