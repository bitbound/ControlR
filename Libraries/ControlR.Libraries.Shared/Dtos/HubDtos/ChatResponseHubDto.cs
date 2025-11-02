namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ChatResponseHubDto(
  Guid SessionId,
  int DesktopSessionProcessId,
  string Message,
  string SenderUsername,
  string ViewerConnectionId,
  DateTimeOffset Timestamp);
