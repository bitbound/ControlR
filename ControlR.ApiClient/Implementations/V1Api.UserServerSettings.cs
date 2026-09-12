using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using UserSettingsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserServerSettings;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<UserSettingsDtos.DecommissionServerResponseDto>> IUserServerSettingsApi.GetDecommissionStatus(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.UserServerSettingsEndpoint}/decommission-status",
        cancellationToken);

      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserSettingsDtos.DecommissionServerResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.");
    });
  }

  async Task<ApiResult<UserSettingsDtos.FileUploadMaxSizeResponseDto>> IUserServerSettingsApi.GetFileUploadMaxSize(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.UserServerSettingsEndpoint}/file-upload-max-size",
        cancellationToken);

      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserSettingsDtos.FileUploadMaxSizeResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.");
    });
  }
}
