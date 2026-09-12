using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

namespace ControlR.ApiClient.Interfaces.V1;

/// <summary>
/// The device file system operations the versioned API publishes. Every method names the tenant it
/// operates for, because the versioned surface addresses a tenant explicitly rather than inferring it
/// from the caller's session. The binary siblings of these operations (file and archive download,
/// upload, log-file contents) stay on the internal surface until the client can carry a streamed
/// result.
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

  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/contents?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DeviceDirectoryContentsResponseDto>> GetDeviceDirectoryContents(
    Guid tenantId,
    DeviceDirectoryContentsRequestDto request,
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

  [ApiRoute($"{HttpConstants.V1.DeviceFileSystemEndpoint}/validate-path/{{deviceId}}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DeviceFilePathValidationResponseDto>> ValidateDeviceFilePath(
    Guid deviceId,
    Guid tenantId,
    ValidateDeviceFilePathRequestDto request,
    CancellationToken cancellationToken = default);
}
