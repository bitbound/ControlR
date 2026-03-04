namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record DesktopPreviewRequestIpcDto(
  Guid RequesterId,
  Guid StreamId,
  int TargetProcessId);
