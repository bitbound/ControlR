namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record FileUploadHubDto(
  Guid StreamId,
  string TargetDirectoryPath,
  string FileName,
  long FileSize,
  bool Overwrite = false);

[MessagePackObject(keyAsPropertyName: true)]
public record FileDownloadHubDto(Guid StreamId, string FilePath);

[MessagePackObject(keyAsPropertyName: true)]
public record FileArchiveDownloadHubDto(Guid StreamId, string ArchiveFileName, string[] TargetPaths);

[MessagePackObject(keyAsPropertyName: true)]
public record FileDownloadResponseHubDto(long FileSize, string FileDisplayName);

[MessagePackObject(keyAsPropertyName: true)]
public record FileDeleteHubDto(string TargetPath);

[MessagePackObject(keyAsPropertyName: true)]
public record CreateDirectoryHubDto(string ParentPath, string DirectoryName);

[MessagePackObject(keyAsPropertyName: true)]
public record ValidateFilePathHubDto(string DirectoryPath, string FileName);
