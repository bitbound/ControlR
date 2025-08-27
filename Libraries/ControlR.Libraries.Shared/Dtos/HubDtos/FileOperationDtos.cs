namespace ControlR.Libraries.Shared.Dtos.HubDtos;

public record FileUploadHubDto(
  Guid StreamId,
  string TargetDirectoryPath,
  string FileName,
  long FileSize);

public record FileDownloadHubDto(
  Guid StreamId,
  string FilePath,
  bool IsDirectory);

public record FileDeleteHubDto(
  string FilePath,
  bool IsDirectory);
