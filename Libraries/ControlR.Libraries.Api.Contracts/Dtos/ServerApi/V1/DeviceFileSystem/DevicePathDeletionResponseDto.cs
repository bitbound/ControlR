namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The deletion the device accepted. A named envelope so consumers have a type to depend on.
/// </summary>
public sealed record DevicePathDeletionResponseDto(
  string Message,
  string FilePath);
