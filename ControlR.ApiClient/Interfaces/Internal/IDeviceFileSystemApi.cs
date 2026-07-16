using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDeviceFileSystemApi
{
  [ApiRoute("POST", "/api/internal/device-file-system/create-directory/{deviceId}")]
  Task<ApiResult> CreateDirectory(CreateDirectoryRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/internal/device-file-system/delete-path/{deviceId}")]
  Task<ApiResult> DeleteFile(FileDeleteRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/device-file-system/download-archive/{deviceId}")]
  Task<ApiResult<ResponseStream>> DownloadArchive(Guid deviceId, DownloadArchiveRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/device-file-system/download/{deviceId}")]
  Task<ApiResult<ResponseStream>> DownloadFile(Guid deviceId, string filePath, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/device-file-system/contents")]
  Task<ApiResult<GetDirectoryContentsResponseDto>> GetDirectoryContents(GetDirectoryContentsRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/device-file-system/logs/{deviceId}/contents")]
  Task<ApiResult<string>> GetLogFileContents(Guid deviceId, GetLogFileContentsRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/device-file-system/logs/{deviceId}")]
  Task<ApiResult<GetLogFilesResponseDto>> GetLogFiles(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/device-file-system/path-segments")]
  Task<ApiResult<PathSegmentsResponseDto>> GetPathSegments(GetPathSegmentsRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/device-file-system/root-drives")]
  Task<ApiResult<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/device-file-system/subdirectories")]
  Task<ApiResult<GetSubdirectoriesResponseDto>> GetSubdirectories(GetSubdirectoriesRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/device-file-system/upload/{deviceId}")]
  Task<ApiResult> UploadFile(Guid deviceId, Stream fileStream, string fileName, string targetSaveDirectory, bool overwrite = false, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/device-file-system/validate-path/{deviceId}")]
  Task<ApiResult<ValidateFilePathResponseDto>> ValidateFilePath(ValidateFilePathRequestDto request, CancellationToken cancellationToken = default);
}
