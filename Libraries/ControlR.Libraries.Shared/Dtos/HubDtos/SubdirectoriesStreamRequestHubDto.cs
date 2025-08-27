namespace ControlR.Libraries.Shared.Dtos.HubDtos;

public record SubdirectoriesStreamRequestHubDto(
  Guid StreamId,
  Guid DeviceId,
  string DirectoryPath);
