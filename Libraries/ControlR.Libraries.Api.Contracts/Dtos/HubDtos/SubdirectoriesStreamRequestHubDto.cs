namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record SubdirectoriesStreamRequestHubDto(
  Guid StreamId,
  Guid DeviceId,
  string DirectoryPath);
