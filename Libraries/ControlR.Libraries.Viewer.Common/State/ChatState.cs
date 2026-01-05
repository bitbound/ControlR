namespace ControlR.Libraries.Viewer.Common.State;

public interface IChatState : IStateBase
{
  ConcurrentList<ChatMessage> ChatMessages { get; }
  DesktopSession? CurrentSession { get; set; }
  bool EnableMultiline { get; set; }
  string NewMessage { get; set; }
  Guid SessionId { get; set; }

  void Clear();
}

public class ChatState(ILogger<ChatState> logger) : StateBase(logger), IChatState
{
  public ConcurrentList<ChatMessage> ChatMessages { get; } = [];
  public DesktopSession? CurrentSession { get; set; }
  public bool EnableMultiline { get; set; }
  public string NewMessage { get; set; } = string.Empty;

  public Guid SessionId { get; set; }

  public void Clear()
  {
    CurrentSession = null;
    ChatMessages.Clear();
    SessionId = Guid.Empty;
    NotifyStateChanged();
  }
}
