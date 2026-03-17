using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.DesktopClient.Common.Options;

public class RemoteControlSessionOptions
{
  private Uri? _webSocketUri;

  public CaptureEncoderType EncoderType { get; set; } = CaptureEncoderType.Image;
  public bool NotifyUser { get; set; }
  public bool RequireConsent { get; set; }
  public Guid SessionId { get; set; }
  public string? ViewerConnectionId { get; set; }
  public string? ViewerName { get; set; }
  public Uri WebSocketUri
  {
    get => _webSocketUri ?? throw new InvalidOperationException("WebSocketUri hasn't been set yet.");
    set => _webSocketUri = value;
  }
}
