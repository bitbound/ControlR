using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> ITenantSettingsApi.DeleteTenantSetting(string settingName, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var url = $"{HttpConstants.TenantSettingsEndpoint}/{settingName}";
      using var response = await _client.DeleteAsync(url, cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult<TenantSettingResponseDto>> ITenantSettingsApi.GetTenantSetting(string settingName, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.TenantSettingsEndpoint}/{settingName}", cancellationToken);
      if (response.StatusCode == HttpStatusCode.NoContent)
      {
        return new TenantSettingResponseDto(Id: null, Name: settingName, Value: null);
      }

      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.", null, response.StatusCode);
    });
  }

  async Task<ApiResult<TenantSettingResponseDto[]>> ITenantSettingsApi.GetTenantSettings(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<TenantSettingResponseDto[]>(HttpConstants.TenantSettingsEndpoint, cancellationToken));
  }

  async Task<ApiResult<TenantSettingResponseDto>> ITenantSettingsApi.SetTenantSetting(TenantSettingRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.TenantSettingsEndpoint, request, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>(cancellationToken);
    });
  }
}
