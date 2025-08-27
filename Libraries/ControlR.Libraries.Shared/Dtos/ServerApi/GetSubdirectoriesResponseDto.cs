using MessagePack;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetSubdirectoriesResponseDto(
  FileSystemEntryDto[] Subdirectories);
