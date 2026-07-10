using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDeviceFileSystemApi
{
  Task<ApiResult> CreateDirectory(CreateDirectoryRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteFile(FileDeleteRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<ResponseStream>> DownloadArchive(Guid deviceId, DownloadArchiveRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<ResponseStream>> DownloadFile(Guid deviceId, string filePath, CancellationToken cancellationToken = default);
  Task<ApiResult<GetDirectoryContentsResponseDto>> GetDirectoryContents(GetDirectoryContentsRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<string>> GetLogFileContents(Guid deviceId, GetLogFileContentsRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<GetLogFilesResponseDto>> GetLogFiles(Guid deviceId, CancellationToken cancellationToken = default);
  Task<ApiResult<PathSegmentsResponseDto>> GetPathSegments(GetPathSegmentsRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<GetSubdirectoriesResponseDto>> GetSubdirectories(GetSubdirectoriesRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> UploadFile(Guid deviceId, Stream fileStream, string fileName, string targetSaveDirectory, bool overwrite = false, CancellationToken cancellationToken = default);
  Task<ApiResult<ValidateFilePathResponseDto>> ValidateFilePath(ValidateFilePathRequestDto request, CancellationToken cancellationToken = default);
}
