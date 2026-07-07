using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> ITenantSettingsApi.DeleteTenantSetting(string settingName, Guid? tenantId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var url = tenantId.HasValue
        ? $"{HttpConstants.TenantSettingsEndpoint}/{settingName}?tenantId={tenantId.Value}"
        : $"{HttpConstants.TenantSettingsEndpoint}/{settingName}";
      using var response = await _client.DeleteAsync(url, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TenantSettingResponseDto>> ITenantSettingsApi.GetTenantSetting(string settingName, Guid? tenantId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var url = tenantId.HasValue
        ? $"{HttpConstants.TenantSettingsEndpoint}/{settingName}?tenantId={tenantId.Value}"
        : $"{HttpConstants.TenantSettingsEndpoint}/{settingName}";
      using var response = await _client.GetAsync(url, cancellationToken);
      if (response.StatusCode == HttpStatusCode.NoContent)
      {
        return new TenantSettingResponseDto(Id: null, Name: settingName, Value: null);
      }

      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.", null, response.StatusCode);
    });
  }

  async Task<ApiResult<TenantSettingsDto>> ITenantSettingsApi.GetTenantSettings(Guid? tenantId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      {
        var url = tenantId.HasValue
          ? $"{HttpConstants.TenantSettingsEndpoint}?tenantId={tenantId.Value}"
          : HttpConstants.TenantSettingsEndpoint;
        return await _client.GetFromJsonAsync<TenantSettingsDto>(url, cancellationToken);
      });
  }

  async Task<ApiResult<TenantSettingResponseDto>> ITenantSettingsApi.SetTenantSetting(TenantSettingRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.TenantSettingsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<TenantSettingsDto>> ITenantSettingsApi.SetTenantSettings(TenantSettingsDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PutAsJsonAsync(HttpConstants.TenantSettingsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantSettingsDto>(cancellationToken);
    });
  }
}
