using MessagePack;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetDirectoryContentsResponseDto(
  FileSystemEntryDto[] Entries,
  bool DirectoryExists);
