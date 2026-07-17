using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDeviceFileSystemApi
{
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/create-directory/{{deviceId}}", "POST")]
  Task<ApiResult> CreateDirectory(CreateDirectoryRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/delete-path/{{deviceId}}", "DELETE")]
  Task<ApiResult> DeleteFile(FileDeleteRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/download-archive/{{deviceId}}", "POST")]
  Task<ApiResult<ResponseStream>> DownloadArchive(Guid deviceId, DownloadArchiveRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/download/{{deviceId}}", "GET")]
  Task<ApiResult<ResponseStream>> DownloadFile(Guid deviceId, string filePath, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/contents", "POST")]
  Task<ApiResult<GetDirectoryContentsResponseDto>> GetDirectoryContents(GetDirectoryContentsRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/logs/{{deviceId}}/contents", "GET")]
  Task<ApiResult<string>> GetLogFileContents(Guid deviceId, GetLogFileContentsRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/logs/{{deviceId}}", "GET")]
  Task<ApiResult<GetLogFilesResponseDto>> GetLogFiles(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/path-segments", "POST")]
  Task<ApiResult<PathSegmentsResponseDto>> GetPathSegments(GetPathSegmentsRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/root-drives", "POST")]
  Task<ApiResult<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/subdirectories", "POST")]
  Task<ApiResult<GetSubdirectoriesResponseDto>> GetSubdirectories(GetSubdirectoriesRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/upload/{{deviceId}}", "POST")]
  Task<ApiResult> UploadFile(Guid deviceId, Stream fileStream, string fileName, string targetSaveDirectory, bool overwrite = false, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/validate-path/{{deviceId}}", "POST")]
  Task<ApiResult<ValidateFilePathResponseDto>> ValidateFilePath(ValidateFilePathRequestDto request, CancellationToken cancellationToken = default);
}
