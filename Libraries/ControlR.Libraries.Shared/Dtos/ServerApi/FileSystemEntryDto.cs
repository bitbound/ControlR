namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record FileSystemEntryDto(
  string Name,
  string FullPath,
  bool IsDirectory,
  long Size,
  DateTimeOffset LastModified,
  bool IsHidden,
  bool CanRead,
  bool CanWrite,
  bool HasSubfolders);
