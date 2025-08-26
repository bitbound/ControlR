using System.Collections.ObjectModel;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient.Models;

public class ChatSession
{
  public ChatWindow? ChatWindow { get; set; }
  public IChatWindowViewModel ViewModel => ChatWindow?.ViewModel ?? throw new InvalidOperationException("ChatWindow is not set.");
  public ObservableCollection<ChatMessageViewModel> Messages => ViewModel.Messages;
  public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
  public Guid SessionId { get; init; }
  public int TargetProcessId { get; init; }
  public int TargetSystemSession { get; init; }
  public string ViewerConnectionId { get; set; } = string.Empty;
}
