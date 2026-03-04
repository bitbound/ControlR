namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetDirectoryContentsRequestDto(
  Guid DeviceId,
  string DirectoryPath);
