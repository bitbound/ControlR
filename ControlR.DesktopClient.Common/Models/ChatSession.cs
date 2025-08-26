using ControlR.Libraries.Shared.Dtos.IpcDtos;

namespace ControlR.DesktopClient.Common.Models;

public class ChatSession
{
  public Guid SessionId { get; init; }
  public int TargetSystemSession { get; init; }
  public int TargetProcessId { get; init; }
  public string ViewerConnectionId { get; set; } = string.Empty;
  public List<ChatMessageIpcDto> Messages { get; init; } = [];
  public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
