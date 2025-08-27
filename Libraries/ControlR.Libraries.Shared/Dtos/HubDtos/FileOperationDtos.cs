namespace ControlR.Libraries.Shared.Dtos.HubDtos;

public record FileUploadHubDto(
  Guid StreamId,
  string TargetDirectoryPath,
  string FileName,
  long FileSize,
  bool Overwrite = false);

public record FileDownloadHubDto(Guid StreamId, string FilePath);

public record FileDeleteHubDto(string TargetPath);

public record CreateDirectoryHubDto(string DirectoryPath);
