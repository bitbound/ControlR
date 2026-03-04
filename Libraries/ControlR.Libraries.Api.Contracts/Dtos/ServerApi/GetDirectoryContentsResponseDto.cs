namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetDirectoryContentsResponseDto(
  FileSystemEntryDto[] Entries,
  bool DirectoryExists);
