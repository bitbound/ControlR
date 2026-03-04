using System.Net.Http.Json;
using System.Net.Http.Headers;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> IDeviceFileSystemApi.CreateDirectory(CreateDirectoryRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DeviceFileSystemEndpoint}/create-directory/{request.DeviceId}", request, cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult> IDeviceFileSystemApi.DeleteFile(FileDeleteRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{HttpConstants.DeviceFileSystemEndpoint}/delete-path/{request.DeviceId}")
      {
        Content = JsonContent.Create(request)
      };
      using var response = await _client.SendAsync(requestMessage, cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult<ResponseStream>> IDeviceFileSystemApi.DownloadFile(Guid deviceId, string filePath, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var response = await _client.GetAsync(
        $"{HttpConstants.DeviceFileSystemEndpoint}/download/{deviceId}?filePath={Uri.EscapeDataString(filePath)}",
        HttpCompletionOption.ResponseHeadersRead,
        cancellationToken);

      response.EnsureSuccessStatusCode();

      var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
      return new ResponseStream(response, stream);
    });
  }

  async Task<ApiResult<GetDirectoryContentsResponseDto>> IDeviceFileSystemApi.GetDirectoryContents(GetDirectoryContentsRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DeviceFileSystemEndpoint}/contents", request, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<GetDirectoryContentsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<string>> IDeviceFileSystemApi.GetLogFileContents(Guid deviceId, GetLogFileContentsRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var encodedPath = Uri.EscapeDataString(request.FilePath);
      using var response = await _client.GetAsync($"{HttpConstants.DeviceFileSystemEndpoint}/logs/{deviceId}/contents?filePath={encodedPath}", cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadAsStringAsync(cancellationToken);
    });
  }

  async Task<ApiResult<GetLogFilesResponseDto>> IDeviceFileSystemApi.GetLogFiles(Guid deviceId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.DeviceFileSystemEndpoint}/logs/{deviceId}", cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<GetLogFilesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<PathSegmentsResponseDto>> IDeviceFileSystemApi.GetPathSegments(GetPathSegmentsRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DeviceFileSystemEndpoint}/path-segments", request, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<PathSegmentsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<GetRootDrivesResponseDto>> IDeviceFileSystemApi.GetRootDrives(GetRootDrivesRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DeviceFileSystemEndpoint}/root-drives", request, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<GetRootDrivesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<GetSubdirectoriesResponseDto>> IDeviceFileSystemApi.GetSubdirectories(GetSubdirectoriesRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DeviceFileSystemEndpoint}/subdirectories", request, cancellationToken);
      response.EnsureSuccessStatusCode();
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
    return await ExecuteApiCall(async () =>
    {
      using var form = new MultipartFormDataContent();
      var fileContent = new StreamContent(fileStream);
      fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

      form.Add(fileContent, "file", fileName);
      form.Add(new StringContent(targetSaveDirectory), "targetSaveDirectory");
      form.Add(new StringContent(overwrite.ToString()), "overwrite");

      using var response = await _client.PostAsync($"{HttpConstants.DeviceFileSystemEndpoint}/upload/{deviceId}", form, cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult<ValidateFilePathResponseDto>> IDeviceFileSystemApi.ValidateFilePath(ValidateFilePathRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DeviceFileSystemEndpoint}/validate-path/{request.DeviceId}", request, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<ValidateFilePathResponseDto>(cancellationToken) ??
        new ValidateFilePathResponseDto(false, "Failed to deserialize response");
    });
  }
}
