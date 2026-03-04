namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetSubdirectoriesResponseDto(
  FileSystemEntryDto[] Subdirectories);
