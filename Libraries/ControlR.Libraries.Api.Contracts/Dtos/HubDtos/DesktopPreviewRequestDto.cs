namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record DesktopPreviewRequestDto(
  Guid RequesterId,
  Guid StreamId,
  int TargetProcessId);
