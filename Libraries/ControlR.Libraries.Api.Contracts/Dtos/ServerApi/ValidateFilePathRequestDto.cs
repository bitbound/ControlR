namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record ValidateFilePathRequestDto(
  Guid DeviceId,
  string DirectoryPath,
  string FileName);
