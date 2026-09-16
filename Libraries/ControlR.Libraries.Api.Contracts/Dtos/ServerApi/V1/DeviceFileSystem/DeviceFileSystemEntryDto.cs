namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// One file system entry as the remote agent reported it. Shared by the directory-contents,
/// root-drives, and subdirectories responses.
/// </summary>
public sealed record DeviceFileSystemEntryDto(
  string Name,
  string FullPath,
  bool IsDirectory,
  long Size,
  DateTimeOffset LastModified,
  bool IsHidden,
  bool CanRead,
  bool CanWrite,
  bool HasSubfolders);
