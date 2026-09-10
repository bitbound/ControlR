using ControlR.ApiClient.Interfaces.V1;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServiceAccounts;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<CreateServiceAccountCredentialResponseDto>> IServerServiceAccountsApi.AddCredential(
    Guid serviceAccountId,
    CreateServiceAccountCredentialRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{serviceAccountId}/credentials", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateServiceAccountCredentialResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<ServerServiceAccountDto>> IServerServiceAccountsApi.Create(
    CreateServerServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.V1.ServerServiceAccountsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ServerServiceAccountDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IServerServiceAccountsApi.Delete(Guid serviceAccountId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{serviceAccountId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<ServerServiceAccountDto>> IServerServiceAccountsApi.Get(
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{serviceAccountId}",
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ServerServiceAccountDto>(cancellationToken);
    });
  }

  async Task<ApiResult<ServerServiceAccountsResponseDto>> IServerServiceAccountsApi.GetAll(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        HttpConstants.V1.ServerServiceAccountsEndpoint,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ServerServiceAccountsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IServerServiceAccountsApi.PurgeCredential(
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{serviceAccountId}/credentials/{credentialId}/purge", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IServerServiceAccountsApi.RevokeCredential(
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{serviceAccountId}/credentials/{credentialId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<ServerServiceAccountDto>> IServerServiceAccountsApi.Update(
    Guid serviceAccountId,
    UpdateServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{serviceAccountId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ServerServiceAccountDto>(cancellationToken);
    });
  }
}
