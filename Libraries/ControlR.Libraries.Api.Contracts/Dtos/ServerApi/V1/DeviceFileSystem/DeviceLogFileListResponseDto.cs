namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The log files the agent has on disk, grouped by the source that wrote them.
/// </summary>
public sealed record DeviceLogFileListResponseDto(
  IReadOnlyList<DeviceLogFileGroupDto> LogFileGroups);
