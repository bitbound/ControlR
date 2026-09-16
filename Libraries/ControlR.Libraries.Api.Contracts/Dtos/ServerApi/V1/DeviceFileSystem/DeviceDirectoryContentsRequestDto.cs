namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// Selects the directory whose entries to list. The route carries no device id for this operation,
/// so the body names the device.
/// </summary>
public sealed record DeviceDirectoryContentsRequestDto(
  Guid DeviceId,
  string DirectoryPath);
