using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Models;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.ViewModels;

public interface IChatWindowViewModel
{
  ChatSession? Session { get; set; }

  Task HandleChatWindowClosed();
}

public class ChatWindowViewModel(
  IChatSessionManager chatSessionManager,
  ILogger<ChatWindowViewModel> logger) : ViewModelBase, IChatWindowViewModel
{
  
  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly ILogger<ChatWindowViewModel> _logger = logger;

  public ChatSession? Session { get; set; }

  public Task HandleChatWindowClosed()
  {
    // Handle chat window closed logic here
    return Task.CompletedTask;
  }
}