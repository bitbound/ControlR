namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record FileDownloadRequestDto(
  Guid DeviceId,
  string FilePath,
  bool IsDirectory);
