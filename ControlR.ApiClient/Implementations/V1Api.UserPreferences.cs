using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using PrefsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<PrefsDtos.UserPreferenceResponseDto>> IUserPreferencesApi.GetPreference(string name, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PrefsDtos.UserPreferenceResponseDto>(
        $"{HttpConstants.V1.UserPreferencesEndpoint}/{name}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<PrefsDtos.UserPreferencesDto>> IUserPreferencesApi.GetPreferences(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PrefsDtos.UserPreferencesDto>(
        $"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<PrefsDtos.UserPreferenceResponseDto>> IUserPreferencesApi.SetPreference(Guid tenantId, PrefsDtos.UserPreferenceRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PrefsDtos.UserPreferenceResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<PrefsDtos.UserPreferencesDto>> IUserPreferencesApi.SetPreferences(Guid tenantId, PrefsDtos.UserPreferencesDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PrefsDtos.UserPreferencesDto>(cancellationToken);
    });
  }
}
