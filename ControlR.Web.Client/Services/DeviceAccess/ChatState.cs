using ControlR.Web.Client.Services.DeviceAccess;

public interface IChatState : IStateBase
{
  bool EnableMultiline { get; set; }
  string NewMessage { get; set; }
  ConcurrentList<ChatMessage> ChatMessages { get; }
  DeviceUiSession? SelectedSession { get; set; }
}

public class ChatState(ILogger<ChatState> logger) : StateBase(logger), IChatState
{
  public bool EnableMultiline { get; set; }
  public string NewMessage { get; set; } = string.Empty;
  public ConcurrentList<ChatMessage> ChatMessages { get; } = [];
  public DeviceUiSession? SelectedSession { get; set; }
}

public class ChatMessage
{
  public bool IsFromViewer { get; set; }
  public string Message { get; set; } = string.Empty;
  public string SenderName { get; set; } = string.Empty;
  public DateTimeOffset Timestamp { get; set; }
}