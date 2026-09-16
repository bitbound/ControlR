namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// Asks the device whether <see cref="DirectoryPath"/> and <see cref="FileName"/> combine into a usable
/// path. The target device comes from the route, so the body carries no device id.
/// </summary>
public sealed record ValidateDeviceFilePathRequestDto(
  string DirectoryPath,
  string FileName);
