namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The agent's log files under one group name, which is the log source that produced them.
/// </summary>
public sealed record DeviceLogFileGroupDto(
  string GroupName,
  IReadOnlyList<DeviceLogFileEntryDto> LogFiles);
