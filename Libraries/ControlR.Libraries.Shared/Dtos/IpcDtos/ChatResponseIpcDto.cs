namespace ControlR.Libraries.Shared.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ChatResponseIpcDto(
  Guid SessionId,
  string Message,
  string SenderUsername,
  string ViewerConnectionId,
  DateTimeOffset Timestamp);
