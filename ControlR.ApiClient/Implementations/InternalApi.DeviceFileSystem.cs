using System.Net.Http.Json;
using System.Net.Http.Headers;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult> IDeviceFileSystemApi.CreateDirectory(CreateDirectoryRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/create-directory/{request.DeviceId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IDeviceFileSystemApi.DeleteFile(FileDeleteRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{HttpConstants.Internal.DeviceFileSystemEndpoint}/delete-path/{request.DeviceId}")
      {
        Content = JsonContent.Create(request)
      };
      using var response = await _client.HttpClient.SendAsync(requestMessage, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<ResponseStream>> IDeviceFileSystemApi.DownloadArchive(Guid deviceId, DownloadArchiveRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{HttpConstants.Internal.DeviceFileSystemEndpoint}/download-archive/{deviceId}")
      {
        Content = JsonContent.Create(request)
      };

      var response = await _client.HttpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();

      var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
      return new ResponseStream(response, stream);
    });
  }

  async Task<ApiResult<ResponseStream>> IDeviceFileSystemApi.DownloadFile(Guid deviceId, string filePath, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.Internal.DeviceFileSystemEndpoint}/download/{deviceId}?filePath={Uri.EscapeDataString(filePath)}",
        HttpCompletionOption.ResponseHeadersRead,
        cancellationToken);

      await response.EnsureSuccessStatusCodeWithDetails();

      var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
      return new ResponseStream(response, stream);
    });
  }

  async Task<ApiResult<GetDirectoryContentsResponseDto>> IDeviceFileSystemApi.GetDirectoryContents(GetDirectoryContentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/contents", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<GetDirectoryContentsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<string>> IDeviceFileSystemApi.GetLogFileContents(Guid deviceId, GetLogFileContentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var encodedPath = Uri.EscapeDataString(request.FilePath);
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/logs/{deviceId}/contents?filePath={encodedPath}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadAsStringAsync(cancellationToken);
    });
  }

  async Task<ApiResult<GetLogFilesResponseDto>> IDeviceFileSystemApi.GetLogFiles(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/logs/{deviceId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<GetLogFilesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<PathSegmentsResponseDto>> IDeviceFileSystemApi.GetPathSegments(GetPathSegmentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/path-segments", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PathSegmentsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<GetRootDrivesResponseDto>> IDeviceFileSystemApi.GetRootDrives(GetRootDrivesRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/root-drives", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<GetRootDrivesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<GetSubdirectoriesResponseDto>> IDeviceFileSystemApi.GetSubdirectories(GetSubdirectoriesRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/subdirectories", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<GetSubdirectoriesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IDeviceFileSystemApi.UploadFile(
    Guid deviceId,
    Stream fileStream,
    string fileName,
    string targetSaveDirectory,
    bool overwrite,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var form = new MultipartFormDataContent();
      var fileContent = new StreamContent(fileStream);
      fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

      form.Add(fileContent, "file", fileName);
      form.Add(new StringContent(targetSaveDirectory), "targetSaveDirectory");
      form.Add(new StringContent(overwrite.ToString()), "overwrite");

      using var response = await _client.HttpClient.PostAsync($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/upload/{deviceId}", form, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<ValidateFilePathResponseDto>> IDeviceFileSystemApi.ValidateFilePath(ValidateFilePathRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.DeviceFileSystemEndpoint}/validate-path/{request.DeviceId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ValidateFilePathResponseDto>(cancellationToken) ??
        new ValidateFilePathResponseDto(false, "Failed to deserialize response");
    });
  }
}
