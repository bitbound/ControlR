using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

namespace ControlR.ApiClient.Interfaces.V1;

/// <summary>
/// The device file system operations the versioned API publishes. Every method names the tenant it
/// operates for, because the versioned surface addresses a tenant explicitly rather than inferring it
/// from the caller's session.
/// </summary>
public interface IDeviceFileSystemApi
{
  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/create-directory/{{deviceId}}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> CreateDeviceDirectory(
    Guid deviceId,
    Guid tenantId,
    CreateDeviceDirectoryRequestDto request,
    CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/delete-path/{{deviceId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult<DevicePathDeletionResponseDto>> DeleteDevicePath(
    Guid deviceId,
    Guid tenantId,
    DeleteDevicePathRequestDto request,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Packs the requested paths into one archive and streams it back. The caller owns the returned
  /// stream and has to dispose it, which releases the connection carrying the archive.
  /// </summary>
  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/download-archive/{{deviceId}}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<ResponseStream>> DownloadDeviceArchive(
    Guid deviceId,
    Guid tenantId,
    DownloadDeviceArchiveRequestDto request,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Streams one file back. The caller owns the returned stream and has to dispose it.
  /// </summary>
  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/download/{{deviceId}}?tenantId={{tenantId}}&filePath={{filePath}}", "GET")]
  Task<ApiResult<ResponseStream>> DownloadDeviceFile(
    Guid deviceId,
    Guid tenantId,
    string filePath,
    CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/contents?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DeviceDirectoryContentsResponseDto>> GetDeviceDirectoryContents(
    Guid tenantId,
    DeviceDirectoryContentsRequestDto request,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Streams the contents of one log file back as text.
  /// </summary>
  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/logs/{{deviceId}}/contents?tenantId={{tenantId}}&filePath={{filePath}}", "GET")]
  Task<ApiResult<string>> GetDeviceLogFileContents(
    Guid deviceId,
    Guid tenantId,
    string filePath,
    CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/logs/{{deviceId}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<DeviceLogFileListResponseDto>> GetDeviceLogFiles(
    Guid deviceId,
    Guid tenantId,
    CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/path-segments?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DevicePathSegmentsResponseDto>> GetDevicePathSegments(
    Guid tenantId,
    DevicePathSegmentsRequestDto request,
    CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/root-drives?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DeviceRootDrivesResponseDto>> GetDeviceRootDrives(
    Guid tenantId,
    DeviceRootDrivesRequestDto request,
    CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/subdirectories?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DeviceSubdirectoriesResponseDto>> GetDeviceSubdirectories(
    Guid tenantId,
    DeviceSubdirectoriesRequestDto request,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Sends one file to the device, which writes it into the named directory. The call closes
  /// <paramref name="fileStream" />, because the multipart body takes it over, so a caller that wants
  /// to send the same file twice has to open it twice.
  /// </summary>
  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/upload/{{deviceId}}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DeviceFileUploadResponseDto>> UploadDeviceFile(
    Guid deviceId,
    Guid tenantId,
    Stream fileStream,
    string fileName,
    string targetSaveDirectory,
    bool overwrite = false,
    CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/validate-path/{{deviceId}}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DeviceFilePathValidationResponseDto>> ValidateDeviceFilePath(
    Guid deviceId,
    Guid tenantId,
    ValidateDeviceFilePathRequestDto request,
    CancellationToken cancellationToken = default);
}
