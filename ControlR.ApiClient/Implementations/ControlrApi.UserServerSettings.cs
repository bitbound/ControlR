using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<DecommissionServerResponseDto>> IUserServerSettingsApi.GetDecommissionStatus(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.UserServerSettingsEndpoint}/decommission-status", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DecommissionServerResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.");
    });
  }

  async Task<ApiResult<long>> IUserServerSettingsApi.GetFileUploadMaxSize(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.UserServerSettingsEndpoint}/file-upload-max-size", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      var dto = await response.Content.ReadFromJsonAsync<FileUploadMaxSizeResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.");
      return dto.MaxFileSize;
    });
  }
}
