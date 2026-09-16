namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The immediate subdirectories of one directory. Unlike the directory-contents response, the agent's
/// directory-exists signal is discarded here.
/// </summary>
public sealed record DeviceSubdirectoriesResponseDto(
  IReadOnlyList<DeviceFileSystemEntryDto> Subdirectories);
