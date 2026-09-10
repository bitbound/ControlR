using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServiceAccounts;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<CreateServiceAccountCredentialResponseDto>> ITenantServiceAccountsApi.AddCredential(
    Guid tenantId,
    Guid serviceAccountId,
    CreateServiceAccountCredentialRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.TenantServiceAccountsEndpoint}/{tenantId}/{serviceAccountId}/credentials",
        request,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateServiceAccountCredentialResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<TenantServiceAccountDto>> ITenantServiceAccountsApi.Create(
    Guid tenantId,
    CreateServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.TenantServiceAccountsEndpoint}/{tenantId}",
        request,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantServiceAccountDto>(cancellationToken);
    });
  }

  async Task<ApiResult> ITenantServiceAccountsApi.Delete(
    Guid tenantId,
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.TenantServiceAccountsEndpoint}/{tenantId}/{serviceAccountId}",
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TenantServiceAccountDto>> ITenantServiceAccountsApi.Get(
    Guid tenantId,
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.TenantServiceAccountsEndpoint}/{tenantId}/{serviceAccountId}",
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantServiceAccountDto>(cancellationToken);
    });
  }

  async Task<ApiResult<TenantServiceAccountsResponseDto>> ITenantServiceAccountsApi.GetAll(
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.TenantServiceAccountsEndpoint}/{tenantId}",
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantServiceAccountsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> ITenantServiceAccountsApi.PurgeCredential(
    Guid tenantId,
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.TenantServiceAccountsEndpoint}/{tenantId}/{serviceAccountId}/credentials/{credentialId}/purge",
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> ITenantServiceAccountsApi.RevokeCredential(
    Guid tenantId,
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.TenantServiceAccountsEndpoint}/{tenantId}/{serviceAccountId}/credentials/{credentialId}",
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TenantServiceAccountDto>> ITenantServiceAccountsApi.Update(
    Guid tenantId,
    Guid serviceAccountId,
    UpdateServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.TenantServiceAccountsEndpoint}/{tenantId}/{serviceAccountId}",
        request,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantServiceAccountDto>(cancellationToken);
    });
  }
}
