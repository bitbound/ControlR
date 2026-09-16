namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The file system entries at the device's roots: drive letters on Windows, a single root on Unix-like
/// platforms.
/// </summary>
public sealed record DeviceRootDrivesResponseDto(
  IReadOnlyList<DeviceFileSystemEntryDto> Drives);
