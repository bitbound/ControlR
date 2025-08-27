namespace ControlR.Web.Client.Services.DeviceAccess.Chat;
public class ChatMessage
{
  public bool IsFromViewer { get; set; }
  public string Message { get; set; } = string.Empty;
  public string SenderName { get; set; } = string.Empty;
  public DateTimeOffset Timestamp { get; set; }
}