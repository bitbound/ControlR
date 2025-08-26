using ControlR.Libraries.Shared.Dtos.IpcDtos;

namespace ControlR.DesktopClient.ViewModels;

public class ChatMessageViewModel : ViewModelBase
{
  public ChatMessageViewModel(ChatMessageIpcDto dto, bool isFromViewer)
  {
    SessionId = dto.SessionId;
    Message = dto.Message;
    SenderName = dto.SenderName;
    SenderEmail = dto.SenderEmail;
    TargetSystemSession = dto.TargetSystemSession;
    TargetProcessId = dto.TargetProcessId;
    ViewerConnectionId = dto.ViewerConnectionId;
    Timestamp = dto.Timestamp;
    IsFromViewer = isFromViewer;
  }

  public ChatMessageViewModel(string message, string senderName, bool isFromViewer)
  {
    Message = message;
    SenderName = senderName;
    IsFromViewer = isFromViewer;
    Timestamp = DateTimeOffset.Now;
    SenderEmail = string.Empty;
    ViewerConnectionId = string.Empty;
  }

  public bool IsFromViewer { get; set; }
  public string Message { get; set; } = string.Empty;
  public string SenderEmail { get; set; } = string.Empty;
  public string SenderName { get; set; } = string.Empty;
  public Guid SessionId { get; set; }
  public int TargetProcessId { get; set; }
  public int TargetSystemSession { get; set; }
  public DateTimeOffset Timestamp { get; set; }
  public string ViewerConnectionId { get; set; } = string.Empty;
}
