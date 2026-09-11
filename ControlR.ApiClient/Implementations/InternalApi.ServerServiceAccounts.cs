using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<InternalDtos.CreateServerServiceAccountCredentialResponseDto>> IServerServiceAccountsApi.AddCredential(Guid serviceAccountId, InternalDtos.CreateServerServiceAccountCredentialRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{serviceAccountId}/credentials", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InternalDtos.CreateServerServiceAccountCredentialResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<InternalDtos.ServerServiceAccountDto>> IServerServiceAccountsApi.Create(InternalDtos.CreateServerServiceAccountRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.ServerServiceAccountsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InternalDtos.ServerServiceAccountDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IServerServiceAccountsApi.Delete(Guid serviceAccountId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{serviceAccountId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<InternalDtos.ServerServiceAccountDto>> IServerServiceAccountsApi.Get(Guid serviceAccountId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<InternalDtos.ServerServiceAccountDto>(
        $"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{serviceAccountId}", cancellationToken));
  }

  async Task<ApiResult<InternalDtos.ServerServiceAccountDto[]>> IServerServiceAccountsApi.GetAll(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<InternalDtos.ServerServiceAccountDto[]>(HttpConstants.Internal.ServerServiceAccountsEndpoint, cancellationToken));
  }

  async Task<ApiResult> IServerServiceAccountsApi.PurgeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{serviceAccountId}/credentials/{credentialId}/purge", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IServerServiceAccountsApi.RevokeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{serviceAccountId}/credentials/{credentialId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<InternalDtos.ServerServiceAccountDto>> IServerServiceAccountsApi.Update(Guid serviceAccountId, InternalDtos.UpdateServerServiceAccountRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{serviceAccountId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InternalDtos.ServerServiceAccountDto>(cancellationToken);
    });
  }
}
