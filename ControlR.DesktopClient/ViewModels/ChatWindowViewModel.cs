
namespace ControlR.DesktopClient.ViewModels;

public interface IChatWindowViewModel
{
  Guid SessionId { get; set; }

  Task HandleChatWindowClosed();
}

public class ChatWindowViewModel : ViewModelBase, IChatWindowViewModel
{
  public Guid SessionId { get; set; }

  public Task HandleChatWindowClosed()
  {
    // Handle chat window closed logic here
    return Task.CompletedTask;
  }
}