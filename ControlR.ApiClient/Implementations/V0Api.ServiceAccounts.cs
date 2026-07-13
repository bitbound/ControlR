using ControlR.ApiClient.Interfaces.V0;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;

namespace ControlR.ApiClient;

internal partial class V0Api
{
  async Task<ApiResult<CreateServiceAccountCredentialResponseDto>> IServiceAccountsApi.AddCredential(
    Guid serviceAccountId,
    CreateServiceAccountCredentialRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V0.ServiceAccountsEndpoint}/{serviceAccountId}/credentials", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateServiceAccountCredentialResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<CreateServiceAccountResponseDto>> IServiceAccountsApi.Create(
    CreateServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.V0.ServiceAccountsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateServiceAccountResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IServiceAccountsApi.Delete(Guid serviceAccountId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.V0.ServiceAccountsEndpoint}/{serviceAccountId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<List<ServiceAccountDto>>> IServiceAccountsApi.GetAll(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        HttpConstants.V0.ServiceAccountsEndpoint,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<List<ServiceAccountDto>>(cancellationToken) ?? [];
    });
  }

  async Task<ApiResult> IServiceAccountsApi.RevokeCredential(
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V0.ServiceAccountsEndpoint}/{serviceAccountId}/credentials/{credentialId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
