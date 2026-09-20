namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The paths to pack into one archive. The route names the device, so the body does not.
/// </summary>
public sealed record DownloadDeviceArchiveRequestDto(
  string ArchiveFileName,
  IReadOnlyList<string> TargetPaths);
