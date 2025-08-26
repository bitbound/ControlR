using System.Collections.ObjectModel;
using Avalonia.Controls;
using ControlR.Libraries.Shared.Dtos.IpcDtos;

namespace ControlR.DesktopClient.Models;

public class ChatSession
{
  public ChatWindow? ChatWindow { get; set; }
  public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
  public ObservableCollection<ChatMessageIpcDto> Messages { get; init; } = [];
  public Guid SessionId { get; init; }
  public int TargetProcessId { get; init; }
  public int TargetSystemSession { get; init; }
  public string ViewerConnectionId { get; set; } = string.Empty;
}
