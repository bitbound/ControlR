namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// Asks the agent how it would split <see cref="TargetPath"/> on its own platform. The route carries
/// no device id for this operation, so the body names the device.
/// </summary>
public sealed record DevicePathSegmentsRequestDto(
  Guid DeviceId,
  string TargetPath);
