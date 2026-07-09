namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[MessagePackObject(keyAsPropertyName: true)]
public record LogFileEntryDto(
  string FileName,
  string FullPath,
  long Size,
  DateTimeOffset LastModified);
