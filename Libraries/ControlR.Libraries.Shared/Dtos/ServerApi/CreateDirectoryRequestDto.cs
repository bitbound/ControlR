namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record CreateDirectoryRequestDto(
  Guid DeviceId,
  string DirectoryPath);
