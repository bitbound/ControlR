namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record LogFileEntryDto(
  string FileName,
  string FullPath,
  long Size,
  DateTimeOffset LastModified);
