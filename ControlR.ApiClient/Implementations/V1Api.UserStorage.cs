using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using StorageDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserStorage;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult> IUserStorageApi.DeleteUserStorageItem(string key, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.UserStorageEndpoint}/{key}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<StorageDtos.UserStorageResponseDto>> IUserStorageApi.GetUserStorageItem(string key, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<StorageDtos.UserStorageResponseDto>(
        $"{HttpConstants.V1.UserStorageEndpoint}/{key}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<StorageDtos.UserStorageResponseDto>> IUserStorageApi.SetUserStorageItem(Guid tenantId, StorageDtos.UserStorageRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.UserStorageEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<StorageDtos.UserStorageResponseDto>(cancellationToken);
    });
  }
}
