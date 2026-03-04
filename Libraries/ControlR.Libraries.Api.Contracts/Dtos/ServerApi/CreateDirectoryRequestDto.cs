namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record CreateDirectoryRequestDto(
  Guid DeviceId,
  string ParentPath,
  string DirectoryName);
