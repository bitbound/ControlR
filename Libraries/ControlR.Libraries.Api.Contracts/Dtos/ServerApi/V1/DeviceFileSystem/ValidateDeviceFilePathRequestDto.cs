namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// Asks whether <see cref="DirectoryPath"/> and <see cref="FileName"/> combine into a usable path. The
/// device comes from the route.
/// </summary>
public sealed record ValidateDeviceFilePathRequestDto(
  string DirectoryPath,
  string FileName);
