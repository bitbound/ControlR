namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// Deletes one path on the device named by the route. There is deliberately no "is a directory" flag:
/// the delete sent to the agent carries only the path, so the flag would describe nothing.
/// </summary>
public sealed record DeleteDevicePathRequestDto(string FilePath);
