using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<CreatePersonalAccessTokenResponseDto>> IPersonalAccessTokensApi.CreatePersonalAccessToken(CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.PersonalAccessTokensEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreatePersonalAccessTokenResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IPersonalAccessTokensApi.DeletePersonalAccessToken(Guid personalAccessTokenId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.PersonalAccessTokensEndpoint}/{personalAccessTokenId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<PersonalAccessTokenDto[]>> IPersonalAccessTokensApi.GetPersonalAccessTokens(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PersonalAccessTokenDto[]>(HttpConstants.PersonalAccessTokensEndpoint, cancellationToken));
  }

  async Task<ApiResult<PersonalAccessTokenDto>> IPersonalAccessTokensApi.UpdatePersonalAccessToken(Guid personalAccessTokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync($"{HttpConstants.PersonalAccessTokensEndpoint}/{personalAccessTokenId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PersonalAccessTokenDto>(cancellationToken);
    });
  }
}
