namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[MessagePackObject(keyAsPropertyName: true)]
public record GetDirectoryContentsResponseDto(
  FileSystemEntryDto[] Entries,
  bool DirectoryExists);
