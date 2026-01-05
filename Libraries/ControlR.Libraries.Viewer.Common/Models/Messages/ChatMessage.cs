namespace ControlR.Libraries.Viewer.Common.Models.Messages;
public class ChatMessage
{
  public bool IsFromViewer { get; set; }
  public string Message { get; set; } = string.Empty;
  public string SenderName { get; set; } = string.Empty;
  public DateTimeOffset Timestamp { get; set; }
}