namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// How the device's agent split the requested path. <see cref="Success"/> is the agent's verdict and
/// <see cref="ErrorMessage"/> explains a false one. <see cref="PathSeparator"/> is the device's own
/// separator, which need not match the one the caller sent.
/// </summary>
public sealed record DevicePathSegmentsResponseDto(
  string? ErrorMessage,
  bool PathExists,
  IReadOnlyList<string> PathSegments,
  string PathSeparator,
  bool Success);
