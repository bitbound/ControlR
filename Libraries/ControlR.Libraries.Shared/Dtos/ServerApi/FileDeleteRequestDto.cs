namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record FileDeleteRequestDto(
  Guid DeviceId,
  string FilePath,
  bool IsDirectory);
