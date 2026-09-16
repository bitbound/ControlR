namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// Deletes one path on the device. The target device comes from the route, so the body carries no
/// device id. The request deliberately has no "is a directory" flag. The delete the server asks the
/// agent to perform carries only the path, so such a flag would describe nothing.
/// </summary>
public sealed record DeleteDevicePathRequestDto(string FilePath);
