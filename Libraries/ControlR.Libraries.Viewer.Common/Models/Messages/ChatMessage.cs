namespace ControlR.Libraries.Viewer.Common.Models.Messages;
public class ChatMessage
{
  public bool IsFromViewer { get; set; }
  public bool IsSystemMessage => Kind != ChatMessageKind.TextSend;
  public ChatMessageKind Kind { get; set; } = ChatMessageKind.TextSend;
  public string Message { get; set; } = string.Empty;
  public string SenderName { get; set; } = string.Empty;
  public DateTimeOffset Timestamp { get; set; }
}