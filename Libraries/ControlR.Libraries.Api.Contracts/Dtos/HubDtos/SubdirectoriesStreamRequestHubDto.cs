namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

public record SubdirectoriesStreamRequestHubDto(
  Guid StreamId,
  Guid DeviceId,
  string DirectoryPath);
