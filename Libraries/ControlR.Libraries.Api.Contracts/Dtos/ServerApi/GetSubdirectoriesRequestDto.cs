namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetSubdirectoriesRequestDto(
  Guid DeviceId,
  string DirectoryPath);
