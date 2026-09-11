using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<InternalDtos.CreateTenantServiceAccountCredentialResponseDto>> ITenantServiceAccountsApi.AddCredential(Guid serviceAccountId, InternalDtos.CreateTenantServiceAccountCredentialRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{serviceAccountId}/credentials", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InternalDtos.CreateTenantServiceAccountCredentialResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<InternalDtos.TenantServiceAccountDto>> ITenantServiceAccountsApi.Create(InternalDtos.CreateTenantServiceAccountRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.TenantServiceAccountsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InternalDtos.TenantServiceAccountDto>(cancellationToken);
    });
  }

  async Task<ApiResult> ITenantServiceAccountsApi.Delete(Guid serviceAccountId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{serviceAccountId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<InternalDtos.TenantServiceAccountDto>> ITenantServiceAccountsApi.Get(Guid serviceAccountId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<InternalDtos.TenantServiceAccountDto>(
        $"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{serviceAccountId}", cancellationToken));
  }

  async Task<ApiResult<InternalDtos.TenantServiceAccountDto[]>> ITenantServiceAccountsApi.GetAll(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<InternalDtos.TenantServiceAccountDto[]>(HttpConstants.Internal.TenantServiceAccountsEndpoint, cancellationToken));
  }

  async Task<ApiResult> ITenantServiceAccountsApi.PurgeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{serviceAccountId}/credentials/{credentialId}/purge", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> ITenantServiceAccountsApi.RevokeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{serviceAccountId}/credentials/{credentialId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<InternalDtos.TenantServiceAccountDto>> ITenantServiceAccountsApi.Update(Guid serviceAccountId, InternalDtos.UpdateTenantServiceAccountRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{serviceAccountId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InternalDtos.TenantServiceAccountDto>(cancellationToken);
    });
  }
}
