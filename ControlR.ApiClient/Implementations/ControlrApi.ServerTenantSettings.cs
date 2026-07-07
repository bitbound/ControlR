using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> IServerTenantSettingsApi.DeleteSetting(Guid tenantId, string name, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.TenantSettingsEndpoint}/server/{tenantId}/{name}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TenantSettingsDto>> IServerTenantSettingsApi.GetAll(Guid tenantId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<TenantSettingsDto>($"{HttpConstants.TenantSettingsEndpoint}/server/{tenantId}", cancellationToken));
  }

  async Task<ApiResult<TenantSettingResponseDto>> IServerTenantSettingsApi.GetSetting(Guid tenantId, string name, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.TenantSettingsEndpoint}/server/{tenantId}/{name}", cancellationToken);
      if (response.StatusCode == HttpStatusCode.NoContent)
      {
        return new TenantSettingResponseDto(Id: null, Name: name, Value: null);
      }

      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.", null, response.StatusCode);
    });
  }

  async Task<ApiResult<TenantSettingResponseDto>> IServerTenantSettingsApi.SetSetting(Guid tenantId, TenantSettingRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.TenantSettingsEndpoint}/server/{tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<TenantSettingsDto>> IServerTenantSettingsApi.SetSettings(Guid tenantId, TenantSettingsDto settings, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PutAsJsonAsync($"{HttpConstants.TenantSettingsEndpoint}/server/{tenantId}", settings, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantSettingsDto>(cancellationToken);
    });
  }
}
