namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[MessagePackObject(keyAsPropertyName: true)]
public record GetSubdirectoriesResponseDto(
  FileSystemEntryDto[] Subdirectories);
