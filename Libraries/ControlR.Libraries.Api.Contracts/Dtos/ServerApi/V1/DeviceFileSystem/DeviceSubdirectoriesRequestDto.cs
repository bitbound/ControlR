namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// Selects the directory whose immediate subdirectories to list. The route carries no device id for
/// this operation, so the body names the device.
/// </summary>
public sealed record DeviceSubdirectoriesRequestDto(
  Guid DeviceId,
  string DirectoryPath);
