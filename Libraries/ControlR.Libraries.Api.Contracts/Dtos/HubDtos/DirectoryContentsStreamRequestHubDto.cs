namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record DirectoryContentsStreamRequestHubDto(
  Guid StreamId,
  Guid DeviceId,
  string DirectoryPath);
