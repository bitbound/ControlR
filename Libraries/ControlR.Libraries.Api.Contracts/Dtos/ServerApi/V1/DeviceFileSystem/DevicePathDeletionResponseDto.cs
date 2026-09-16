namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The deletion the device accepted. The response is a named envelope rather than the ad hoc body the
/// deprecated internal endpoint returns, so the contract has a type consumers can depend on.
/// </summary>
public sealed record DevicePathDeletionResponseDto(
  string Message,
  string FilePath);
