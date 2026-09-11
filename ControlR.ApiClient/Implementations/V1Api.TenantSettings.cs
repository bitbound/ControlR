using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using SettingsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.TenantSettings;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult> ITenantSettingsApi.DeleteTenantSetting(string settingName, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.TenantSettingsEndpoint}/{settingName}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<SettingsDtos.TenantSettingResponseDto>> ITenantSettingsApi.GetTenantSetting(string settingName, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<SettingsDtos.TenantSettingResponseDto>(
        $"{HttpConstants.V1.TenantSettingsEndpoint}/{settingName}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<SettingsDtos.TenantSettingsDto>> ITenantSettingsApi.GetTenantSettings(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<SettingsDtos.TenantSettingsDto>(
        $"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<SettingsDtos.TenantSettingResponseDto>> ITenantSettingsApi.SetTenantSetting(Guid tenantId, SettingsDtos.TenantSettingRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<SettingsDtos.TenantSettingResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<SettingsDtos.TenantSettingsDto>> ITenantSettingsApi.SetTenantSettings(Guid tenantId, SettingsDtos.TenantSettingsDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<SettingsDtos.TenantSettingsDto>(cancellationToken);
    });
  }
}
