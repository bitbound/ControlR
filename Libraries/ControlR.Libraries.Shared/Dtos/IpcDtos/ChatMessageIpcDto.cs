namespace ControlR.Libraries.Shared.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ChatMessageIpcDto(
  Guid SessionId,
  string Message,
  string SenderName,
  string SenderEmail,
  int TargetSystemSession,
  int TargetProcessId,
  string ViewerConnectionId,
  DateTimeOffset Timestamp);
