namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetSubdirectoriesRequestDto(
  Guid DeviceId,
  string DirectoryPath);
