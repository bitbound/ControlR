namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record FileUploadRequestDto(
  Guid DeviceId,
  string TargetDirectoryPath,
  string FileName,
  long FileSize,
  Guid StreamId);
