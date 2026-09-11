using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using PATDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<PATDtos.CreatePersonalAccessTokenResponseDto>> IPersonalAccessTokensApi.CreatePersonalAccessToken(Guid tenantId, PATDtos.CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.PersonalAccessTokensEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PATDtos.CreatePersonalAccessTokenResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IPersonalAccessTokensApi.DeletePersonalAccessToken(Guid id, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.PersonalAccessTokensEndpoint}/{id}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<PATDtos.PersonalAccessTokensResponseDto>> IPersonalAccessTokensApi.GetPersonalAccessTokens(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PATDtos.PersonalAccessTokensResponseDto>(
        $"{HttpConstants.V1.PersonalAccessTokensEndpoint}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<PATDtos.PersonalAccessTokenResponseDto>> IPersonalAccessTokensApi.UpdatePersonalAccessToken(Guid id, Guid tenantId, PATDtos.UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.PersonalAccessTokensEndpoint}/{id}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PATDtos.PersonalAccessTokenResponseDto>(cancellationToken);
    });
  }
}
