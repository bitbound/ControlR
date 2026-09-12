namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// Asks for the file system entries at the device's roots. The route carries no device id for this
/// operation, so the body names the device.
/// </summary>
public sealed record DeviceRootDrivesRequestDto(Guid DeviceId);
