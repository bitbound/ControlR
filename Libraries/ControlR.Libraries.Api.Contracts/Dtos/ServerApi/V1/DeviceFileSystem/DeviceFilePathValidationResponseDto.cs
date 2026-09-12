namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The device's answer to a path-validation probe. An answer that the path is invalid is still a
/// successful response, so <see cref="IsValid"/> being false is not an error.
/// </summary>
public sealed record DeviceFilePathValidationResponseDto(
  bool IsValid,
  string ErrorMessage = "");
