using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<DecommissionServerResponseDto>> IUserServerSettingsApi.GetDecommissionStatus(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.Internal.UserServerSettingsEndpoint}/decommission-status", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DecommissionServerResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.");
    });
  }

  async Task<ApiResult<long>> IUserServerSettingsApi.GetFileUploadMaxSize(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.Internal.UserServerSettingsEndpoint}/file-upload-max-size", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      var dto = await response.Content.ReadFromJsonAsync<FileUploadMaxSizeResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.");
      return dto.MaxFileSize;
    });
  }
}
