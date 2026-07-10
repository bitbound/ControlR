using System.Net;
using ControlR.ApiClient.Interfaces.Internal;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult> ITenantSettingsApi.DeleteTenantSetting(string settingName, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.Internal.TenantSettingsEndpoint}/{settingName}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TenantSettingResponseDto>> ITenantSettingsApi.GetTenantSetting(string settingName, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.Internal.TenantSettingsEndpoint}/{settingName}", cancellationToken);
      if (response.StatusCode == HttpStatusCode.NoContent)
      {
        return new TenantSettingResponseDto(Id: null, Name: settingName, Value: null);
      }

      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.", null, response.StatusCode);
    });
  }

  async Task<ApiResult<TenantSettingsDto>> ITenantSettingsApi.GetTenantSettings(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<TenantSettingsDto>(HttpConstants.Internal.TenantSettingsEndpoint, cancellationToken));
  }

  async Task<ApiResult<TenantSettingResponseDto>> ITenantSettingsApi.SetTenantSetting(TenantSettingRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.TenantSettingsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<TenantSettingsDto>> ITenantSettingsApi.SetTenantSettings(TenantSettingsDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(HttpConstants.Internal.TenantSettingsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantSettingsDto>(cancellationToken);
    });
  }
}
