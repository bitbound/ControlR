namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record DesktopPreviewResponseIpcDto(
  byte[] JpegData,
  bool IsSuccess,
  string? ErrorMessage = null);
