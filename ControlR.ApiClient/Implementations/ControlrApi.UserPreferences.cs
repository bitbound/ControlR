using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<UserPreferenceResponseDto>> IUserPreferencesApi.GetUserPreference(string preferenceName, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.UserPreferencesEndpoint}/{preferenceName}", cancellationToken);
      if (response.StatusCode == HttpStatusCode.NoContent)
      {
        return new UserPreferenceResponseDto(Id: null, Name: preferenceName, Value: null);
      }

      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<UserPreferenceResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.", null, response.StatusCode);
    });
  }

  async Task<ApiResult<UserPreferenceResponseDto[]>> IUserPreferencesApi.GetUserPreferences(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<UserPreferenceResponseDto[]>(HttpConstants.UserPreferencesEndpoint, cancellationToken));
  }

  async Task<ApiResult<UserPreferenceResponseDto>> IUserPreferencesApi.SetUserPreference(UserPreferenceRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.UserPreferencesEndpoint, request, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<UserPreferenceResponseDto>(cancellationToken);
    });
  }
}
