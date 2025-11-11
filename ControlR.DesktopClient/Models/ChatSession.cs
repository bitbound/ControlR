using System.Collections.ObjectModel;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient.Models;

/// <summary>
/// Represents a chat session with its core data.
/// This is a pure data model without UI concerns.
/// </summary>
public class ChatSession
{
  public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
  public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
  public Guid SessionId { get; init; }
  public int TargetProcessId { get; init; }
  public int TargetSystemSession { get; init; }
  public string ViewerConnectionId { get; set; } = string.Empty;
  public string ViewerName { get; init; } = string.Empty;
}
