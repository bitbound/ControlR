namespace ControlR.Libraries.Shared.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CloseChatSessionIpcDto(
  Guid SessionId,
  int TargetProcessId);
