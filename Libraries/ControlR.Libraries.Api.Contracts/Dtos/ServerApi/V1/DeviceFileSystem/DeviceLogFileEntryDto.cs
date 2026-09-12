namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// A single log file the agent found on disk.
/// </summary>
public sealed record DeviceLogFileEntryDto(
  string FileName,
  string FullPath,
  long Size,
  DateTimeOffset LastModified);
