namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// How the device's agent split the requested path. <see cref="Success"/> is the agent's verdict, and
/// <see cref="ErrorMessage"/> explains a false one. <see cref="PathSeparator"/> is the separator the
/// device itself uses, which is not the separator the caller sent the request with.
/// </summary>
public sealed record DevicePathSegmentsResponseDto(
  string? ErrorMessage,
  bool PathExists,
  IReadOnlyList<string> PathSegments,
  string PathSeparator,
  bool Success);
