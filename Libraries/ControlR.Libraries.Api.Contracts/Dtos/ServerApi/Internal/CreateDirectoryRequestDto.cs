namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record CreateDirectoryRequestDto(
  Guid DeviceId,
  string ParentPath,
  string DirectoryName);
