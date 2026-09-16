namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[MessagePackObject(keyAsPropertyName: true)]
public record GetDirectoryContentsResponseDto(
  IReadOnlyList<FileSystemEntryDto> Items,
  bool DirectoryExists);
