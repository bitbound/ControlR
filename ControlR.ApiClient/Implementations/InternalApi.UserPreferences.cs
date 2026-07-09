using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<UserPreferenceResponseDto>> IUserPreferencesApi.GetUserPreference(string preferenceName, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.Internal.UserPreferencesEndpoint}/{preferenceName}", cancellationToken);
      if (response.StatusCode == HttpStatusCode.NoContent)
      {
        return new UserPreferenceResponseDto(Id: null, Name: preferenceName, Value: null);
      }

      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserPreferenceResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.", null, response.StatusCode);
    });
  }

  async Task<ApiResult<UserPreferencesDto>> IUserPreferencesApi.GetUserPreferences(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<UserPreferencesDto>(HttpConstants.Internal.UserPreferencesEndpoint, cancellationToken));
  }

  async Task<ApiResult<UserPreferenceResponseDto>> IUserPreferencesApi.SetUserPreference(UserPreferenceRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.UserPreferencesEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserPreferenceResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<UserPreferencesDto>> IUserPreferencesApi.SetUserPreferences(UserPreferencesDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(HttpConstants.Internal.UserPreferencesEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserPreferencesDto>(cancellationToken);
    });
  }
}
