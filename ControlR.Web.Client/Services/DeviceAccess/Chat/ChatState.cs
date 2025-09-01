namespace ControlR.Web.Client.Services.DeviceAccess.Chat;

public interface IChatState : IStateBase
{
  ConcurrentList<ChatMessage> ChatMessages { get; }
  Guid SessionId { get; set; }
  bool EnableMultiline { get; set; }
  string NewMessage { get; set; }
  DeviceUiSession? CurrentSession { get; set; }

  void Clear();
}

public class ChatState(ILogger<ChatState> logger) : StateBase(logger), IChatState
{
  public ConcurrentList<ChatMessage> ChatMessages { get; } = [];

  public Guid SessionId { get; set; }
  public bool EnableMultiline { get; set; }
  public string NewMessage { get; set; } = string.Empty;
  public DeviceUiSession? CurrentSession { get; set; }

  public void Clear()
  {
    CurrentSession = null;
    ChatMessages.Clear();
    SessionId = Guid.Empty;
    NotifyStateChanged();
  }
}
