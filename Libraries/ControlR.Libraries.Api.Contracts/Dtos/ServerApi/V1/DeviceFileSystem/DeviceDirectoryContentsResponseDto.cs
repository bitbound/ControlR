namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// A directory listing. <see cref="DirectoryExists"/> is the agent's own signal, which distinguishes
/// an empty directory from a missing one.
/// </summary>
public sealed record DeviceDirectoryContentsResponseDto(
  IReadOnlyList<DeviceFileSystemEntryDto> Items,
  bool DirectoryExists);
