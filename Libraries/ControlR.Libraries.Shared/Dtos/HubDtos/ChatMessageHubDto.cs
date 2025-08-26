namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ChatMessageHubDto(
  Guid DeviceId,
  Guid SessionId,
  string Message,
  string SenderName,
  string SenderEmail,
  int TargetSystemSession,
  int TargetProcessId,
  DateTimeOffset Timestamp,
  string ViewerConnectionId = "");
