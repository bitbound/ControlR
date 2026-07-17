namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record FileUploadRequestDto(
  Guid DeviceId,
  string TargetDirectoryPath,
  string FileName,
  long FileSize,
  Guid StreamId);
