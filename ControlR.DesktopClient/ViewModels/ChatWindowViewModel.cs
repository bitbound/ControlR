using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Models;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.ViewModels;

public interface IChatWindowViewModel
{
  string ChatTitle { get; }
  ObservableCollection<ChatMessageViewModel> Messages { get; }
  string NewMessage { get; set; }
  ICommand SendMessageCommand { get; }
  ChatSession? Session { get; set; }

  Task HandleChatWindowClosed();

  Task SendMessage();
}

public class ChatWindowViewModel(
  IChatSessionManager chatSessionManager,
  IToaster toaster,
  ILogger<ChatWindowViewModel> logger) : ViewModelBase, IChatWindowViewModel
{

  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly ILogger<ChatWindowViewModel> _logger = logger;
  private readonly IToaster _toaster = toaster;

  private string _newMessage = string.Empty;
  private ChatSession? _session;

  public string ChatTitle => Session is not null
    ? string.Format(Localization.ChatWindowTitle, Session.ViewerConnectionId)
    : Localization.ChatWindowDefaultTitle;

  public ObservableCollection<ChatMessageViewModel> Messages =>
    Session?.Messages ?? [];

  public string NewMessage
  {
    get => _newMessage;
    set => SetProperty(ref _newMessage, value);
  }

  public ICommand SendMessageCommand => new AsyncRelayCommand(SendMessage);

  public ChatSession? Session
  {
    get => _session;
    set
    {
      if (SetProperty(ref _session, value))
      {
        OnPropertyChanged(nameof(ChatTitle));
        OnPropertyChanged(nameof(Messages));
      }
    }
  }

  public async Task HandleChatWindowClosed()
  {
    if (Session is null)
    {
      return;
    }

    // Send a system message to notify the viewer that the chat window was closed
    var systemMessage = Localization.ChatWindowClosedSystemMessage;
    var success = await _chatSessionManager.SendResponse(Session.SessionId, systemMessage);

    await _chatSessionManager.CloseChatSession(Session.SessionId, false);

    if (success)
    {
      _logger.LogDebug("Chat window closed system message sent for session {SessionId}", Session.SessionId);
    }
    else
    {
      _logger.LogWarning("Failed to send chat window closed system message for session {SessionId}", Session.SessionId);
    }
  }

  public async Task SendMessage()
  {
    if (Session is null || string.IsNullOrWhiteSpace(NewMessage))
    {
      return;
    }

    var message = NewMessage.Trim();

    // Send the response via the chat session manager
    var success = await _chatSessionManager.SendResponse(Session.SessionId, message);

    if (success)
    {
      // Add the message to our local collection immediately
      var messageViewModel = new ChatMessageViewModel(message, Localization.You, false);
      Messages.Add(messageViewModel);
      NewMessage = string.Empty;
      _logger.LogDebug("Message sent successfully for session {SessionId}", Session.SessionId);
    }
    else
    {
      _logger.LogWarning("Failed to send message for session {SessionId}", Session.SessionId);
      await _toaster.ShowToast("Send Failure", "Failed to send message", ToastIcon.Error);
    }
  }
}