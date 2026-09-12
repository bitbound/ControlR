namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The file system entries at the device's roots, which are the drive letters on Windows and the
/// single root on Unix-like platforms.
/// </summary>
public sealed record DeviceRootDrivesResponseDto(
  IReadOnlyList<DeviceFileSystemEntryDto> Drives);
