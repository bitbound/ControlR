namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record FileDownloadRequestDto(
  Guid DeviceId,
  string FilePath,
  bool IsDirectory);
