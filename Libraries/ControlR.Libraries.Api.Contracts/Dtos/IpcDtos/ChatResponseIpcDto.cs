namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ChatResponseIpcDto(
  Guid SessionId,
  int DesktopUiProcessId,
  string Message,
  string SenderUsername,
  string ViewerConnectionId,
  DateTimeOffset Timestamp);
