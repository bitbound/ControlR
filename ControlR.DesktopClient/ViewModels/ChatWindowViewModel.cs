using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Models;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.ViewModels;

public interface IChatWindowViewModel
{
  ChatSession? Session { get; set; }
  string NewMessage { get; set; }
  string ChatTitle { get; }
  ObservableCollection<ChatMessageViewModel> Messages { get; }
  ICommand SendMessageCommand { get; }
  Task HandleChatWindowClosed();
  Task SendMessage();
}

public class ChatWindowViewModel(
  IChatSessionManager chatSessionManager,
  IToaster toaster,
  ILogger<ChatWindowViewModel> logger) : ViewModelBase, IChatWindowViewModel
{
  
  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly IToaster _toaster = toaster;
  private readonly ILogger<ChatWindowViewModel> _logger = logger;
  private string _newMessage = string.Empty;

  public ChatSession? Session { get; set; }

  public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

  public string NewMessage 
  { 
    get => _newMessage;
    set => SetProperty(ref _newMessage, value);
  }

  public string ChatTitle => Session is not null 
    ? string.Format(Localization.ChatWindowTitle, Session.ViewerConnectionId)
    : Localization.ChatWindowDefaultTitle;

  public ICommand SendMessageCommand => new AsyncRelayCommand(SendMessage);

  public async Task SendMessage()
  {
    if (Session is null || string.IsNullOrWhiteSpace(NewMessage))
    {
      return;
    }

    var message = NewMessage.Trim();
    
    // Add the message to our local collection immediately
    var messageViewModel = new ChatMessageViewModel(message, "You", false);
    Messages.Add(messageViewModel);

    // Send the response via the chat session manager
    var success = await _chatSessionManager.SendResponse(Session.SessionId, message);

    if (success)
    {
      NewMessage = string.Empty;
      _logger.LogDebug("Message sent successfully for session {SessionId}", Session.SessionId);
    }
    else
    {
      // Remove the message if sending failed
      Messages.Remove(messageViewModel);
      _logger.LogWarning("Failed to send message for session {SessionId}", Session.SessionId);
      await _toaster.ShowToast("Send Failure", "Failed to send message", ToastIcon.Error);
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
}