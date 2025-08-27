namespace ControlR.Libraries.Shared.Dtos.HubDtos;

public record DirectoryContentsStreamRequestHubDto(
  Guid StreamId,
  Guid DeviceId,
  string DirectoryPath);
