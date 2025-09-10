namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetRootDrivesResponseDto(
  FileSystemEntryDto[] Drives);
