namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record FileDeleteRequestDto(
  Guid DeviceId,
  string FilePath,
  bool IsDirectory);
