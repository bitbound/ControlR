using System.Net.Http.Headers;
using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult> IDeviceFileSystemApi.CreateDeviceDirectory(
    Guid deviceId,
    Guid tenantId,
    CreateDeviceDirectoryRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/create-directory/{deviceId}?tenantId={tenantId}",
        request,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<DevicePathDeletionResponseDto>> IDeviceFileSystemApi.DeleteDevicePath(
    Guid deviceId,
    Guid tenantId,
    DeleteDevicePathRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var requestMessage = new HttpRequestMessage(
        HttpMethod.Delete,
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/delete-path/{deviceId}?tenantId={tenantId}")
      {
        Content = JsonContent.Create(request)
      };

      using var response = await _client.HttpClient.SendAsync(requestMessage, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DevicePathDeletionResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<ResponseStream>> IDeviceFileSystemApi.DownloadDeviceArchive(
    Guid deviceId,
    Guid tenantId,
    DownloadDeviceArchiveRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var requestMessage = new HttpRequestMessage(
        HttpMethod.Post,
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/download-archive/{deviceId}?tenantId={tenantId}")
      {
        Content = JsonContent.Create(request)
      };

      // ResponseStream owns the response, because the caller reads the body after this method returns.
      // A response that never becomes a ResponseStream is this method's to release.
      var response = await _client.HttpClient.SendAsync(
        requestMessage,
        HttpCompletionOption.ResponseHeadersRead,
        cancellationToken);
      try
      {
        await response.EnsureSuccessStatusCodeWithDetails();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new ResponseStream(response, stream);
      }
      catch
      {
        response.Dispose();
        throw;
      }
    });
  }

  async Task<ApiResult<ResponseStream>> IDeviceFileSystemApi.DownloadDeviceFile(
    Guid deviceId,
    Guid tenantId,
    string filePath,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var encodedFilePath = Uri.EscapeDataString(filePath);
      var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/download/{deviceId}?tenantId={tenantId}&filePath={encodedFilePath}",
        HttpCompletionOption.ResponseHeadersRead,
        cancellationToken);
      try
      {
        await response.EnsureSuccessStatusCodeWithDetails();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new ResponseStream(response, stream);
      }
      catch
      {
        response.Dispose();
        throw;
      }
    });
  }

  async Task<ApiResult<DeviceDirectoryContentsResponseDto>> IDeviceFileSystemApi.GetDeviceDirectoryContents(
    Guid tenantId,
    DeviceDirectoryContentsRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/contents?tenantId={tenantId}",
        request,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceDirectoryContentsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<string>> IDeviceFileSystemApi.GetDeviceLogFileContents(
    Guid deviceId,
    Guid tenantId,
    string filePath,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var encodedFilePath = Uri.EscapeDataString(filePath);
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/logs/{deviceId}/contents?tenantId={tenantId}&filePath={encodedFilePath}",
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadAsStringAsync(cancellationToken);
    });
  }

  async Task<ApiResult<DeviceLogFileListResponseDto>> IDeviceFileSystemApi.GetDeviceLogFiles(
    Guid deviceId,
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<DeviceLogFileListResponseDto>(
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/logs/{deviceId}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<DevicePathSegmentsResponseDto>> IDeviceFileSystemApi.GetDevicePathSegments(
    Guid tenantId,
    DevicePathSegmentsRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/path-segments?tenantId={tenantId}",
        request,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DevicePathSegmentsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DeviceRootDrivesResponseDto>> IDeviceFileSystemApi.GetDeviceRootDrives(
    Guid tenantId,
    DeviceRootDrivesRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/root-drives?tenantId={tenantId}",
        request,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceRootDrivesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DeviceSubdirectoriesResponseDto>> IDeviceFileSystemApi.GetDeviceSubdirectories(
    Guid tenantId,
    DeviceSubdirectoriesRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/subdirectories?tenantId={tenantId}",
        request,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceSubdirectoriesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DeviceFileUploadResponseDto>> IDeviceFileSystemApi.UploadDeviceFile(
    Guid deviceId,
    Guid tenantId,
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

      using var response = await _client.HttpClient.PostAsync(
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/upload/{deviceId}?tenantId={tenantId}",
        form,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceFileUploadResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DeviceFilePathValidationResponseDto>> IDeviceFileSystemApi.ValidateDeviceFilePath(
    Guid deviceId,
    Guid tenantId,
    ValidateDeviceFilePathRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/validate-path/{deviceId}?tenantId={tenantId}",
        request,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceFilePathValidationResponseDto>(cancellationToken);
    });
  }
}
