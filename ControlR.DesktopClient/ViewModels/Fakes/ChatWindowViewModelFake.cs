using System.Collections.ObjectModel;
using System.Windows.Input;
using ControlR.DesktopClient.Models;

namespace ControlR.DesktopClient.ViewModels.Fakes;

public class ChatWindowViewModelFake : IChatWindowViewModel
{
  public ChatWindowViewModelFake()
  {
    var fakeSession = new ChatSession
    {
      SessionId = Guid.NewGuid(),
      ViewerConnectionId = "viewer@example.com",
      TargetSystemSession = 1,
      TargetProcessId = 1234,
    };

    // Add some fake messages
    fakeSession.Messages.Add(new ChatMessageViewModel(
      "Hello! I need some help with my computer.", 
      "viewer@example.com", 
      true));
    
    fakeSession.Messages.Add(new ChatMessageViewModel(
      "Hi there! I'd be happy to help. What's the issue?", 
      Environment.UserName, 
      false));
    
    fakeSession.Messages.Add(new ChatMessageViewModel(
      "My screen keeps flickering and I can't figure out why.", 
      "viewer@example.com", 
      true));

    Session = fakeSession;
    NewMessage = "Let me take a look at that for you...";
  }

#pragma warning disable CS0067 // Event is never used - this is a fake/mock implementation
  public event EventHandler? MessagesChanged;
#pragma warning restore CS0067

  public string ChatTitle => "Chat with viewer@example.com";
  public ObservableCollection<ChatMessageViewModel> Messages => Session?.Messages ?? [];
  public string NewMessage { get; set; } = string.Empty;
  public ICommand SendMessageCommand => new RelayCommand(() => { });

  public ChatSession? Session { get; set; }

  public Task HandleChatWindowClosed() => Task.CompletedTask;
  public Task SendMessage() => Task.CompletedTask;
}
