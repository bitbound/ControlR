namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record DownloadArchiveRequestDto(
  string ArchiveFileName,
  string[] TargetPaths);