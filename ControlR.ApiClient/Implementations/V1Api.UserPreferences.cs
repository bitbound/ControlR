using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<UserPreferenceResponseDto>> IUserPreferencesApi.GetPreference(string name, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<UserPreferenceResponseDto>(
        $"{HttpConstants.V1.UserPreferencesEndpoint}/{name}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<UserPreferencesDto>> IUserPreferencesApi.GetPreferences(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<UserPreferencesDto>(
        $"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<UserPreferenceResponseDto>> IUserPreferencesApi.SetPreference(Guid tenantId, UserPreferenceRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserPreferenceResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<UserPreferencesDto>> IUserPreferencesApi.SetPreferences(Guid tenantId, UserPreferencesDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserPreferencesDto>(cancellationToken);
    });
  }
}
