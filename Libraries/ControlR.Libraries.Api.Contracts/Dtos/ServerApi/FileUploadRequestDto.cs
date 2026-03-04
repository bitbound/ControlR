namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record FileUploadRequestDto(
  Guid DeviceId,
  string TargetDirectoryPath,
  string FileName,
  long FileSize,
  Guid StreamId);
