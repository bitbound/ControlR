using ControlR.Libraries.Avalonia.Controls.Snackbar;

namespace ControlR.Viewer.Avalonia.ViewModels.Fakes;

internal class ChatViewModelFake : ViewModelBaseFake<ChatView>, IChatViewModel
{
  public ChatViewModelFake()
  {
    CurrentState = ChatPageState.ChatActive;

    DesktopSessions =
    [
      new ChatDesktopCardViewModel(new DesktopSession
      {
        Name = "Console Session",
        Username = "jared",
        SystemSessionId = 1,
        ProcessId = 12067,
        Type = DesktopSessionType.Console
      })
    ];

    ChatMessages =
    [
      new ChatMessage { Message = "Hey, can you check the logs on the server?", SenderName = "jared", Timestamp = DateTimeOffset.Now.AddMinutes(-5), IsFromViewer = true },
      new ChatMessage { Message = "Sure, give me a moment.", SenderName = "DESKTOP-ABC", Timestamp = DateTimeOffset.Now.AddMinutes(-4), IsFromViewer = false },
      new ChatMessage { Message = "Found a few errors, sending the dump.", SenderName = "jared", Timestamp = DateTimeOffset.Now.AddMinutes(-3), IsFromViewer = true }
    ];
  }

  public string? AlertMessage { get; set; }
  public SnackbarSeverity AlertSeverity => SnackbarSeverity.Info;
  public ObservableCollection<ChatMessage> ChatMessages { get; } = [];
  public string ChatTitle => "Chat with jared (Console Session)";
  public IAsyncRelayCommand CloseChatCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
  public int CommandInputHeight => EnableMultiline ? 120 : 40;
  public ChatPageState CurrentState { get; set; }
  public ObservableCollection<IChatDesktopCardViewModel> DesktopSessions { get; }
  public string DesktopSessionTitle => "Desktop Session(s) on ControlR Device";
  public bool EnableMultiline { get; set; }
  public bool HasDesktopSessions => DesktopSessions.Count > 0;
  public string? LoadingMessage { get; set; }
  public string NewMessage { get; set; } = string.Empty;
  public IAsyncRelayCommand RefreshSessionsCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
  public IAsyncRelayCommand ReloadCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
  public IAsyncRelayCommand SendMessageCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);

  public void ReloadMessages()
  {
    var chats = ChatMessages.ToList();
    ChatMessages.Clear();
    ChatMessages.AddRange(chats);
  }

  public Task StartChat(DesktopSession session) => Task.CompletedTask;
}
