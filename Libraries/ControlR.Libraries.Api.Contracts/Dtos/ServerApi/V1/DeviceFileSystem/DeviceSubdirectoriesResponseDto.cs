namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

/// <summary>
/// The immediate subdirectories of one directory. Unlike the directory-contents response, this carries
/// no directory-exists signal. The agent's signal is discarded for this operation.
/// </summary>
public sealed record DeviceSubdirectoriesResponseDto(
  IReadOnlyList<DeviceFileSystemEntryDto> Subdirectories);
