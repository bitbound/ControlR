namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record FileDeleteRequestDto(
  Guid DeviceId,
  string FilePath,
  bool IsDirectory);
