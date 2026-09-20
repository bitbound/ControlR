namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The upload the device accepted. A named envelope so consumers have a type to depend on.
/// </summary>
public sealed record DeviceFileUploadResponseDto(
  string Message,
  string FileName);
