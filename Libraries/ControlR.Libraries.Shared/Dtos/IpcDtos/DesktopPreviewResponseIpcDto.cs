namespace ControlR.Libraries.Shared.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record DesktopPreviewResponseIpcDto(
  byte[] JpegData,
  bool IsSuccess,
  string? ErrorMessage = null);
