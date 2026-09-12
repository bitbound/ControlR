namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// Creates a directory inside the device's <see cref="ParentPath"/>. The target device comes from the
/// route, so the body carries no device id.
/// </summary>
public sealed record CreateDeviceDirectoryRequestDto(
  string ParentPath,
  string DirectoryName);
