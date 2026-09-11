using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V1Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using IKDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<V1Dtos.CreateInstallerKeyResponseDto>> IInstallerKeysApi.CreateInstallerKey(V1Dtos.CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.V1.InstallerKeysEndpoint, dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V1Dtos.CreateInstallerKeyResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInstallerKeysApi.DeleteInstallerKey(Guid keyId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.V1.InstallerKeysEndpoint}/{keyId}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<IKDtos.InstallerKeysResponseDto>> IInstallerKeysApi.GetAllInstallerKeys(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.V1.InstallerKeysEndpoint}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<IKDtos.InstallerKeysResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<IKDtos.InstallerKeyUsagesResponseDto>> IInstallerKeysApi.GetInstallerKeyUsages(Guid keyId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.V1.InstallerKeysEndpoint}/{keyId}/usages?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<IKDtos.InstallerKeyUsagesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInstallerKeysApi.RenameInstallerKey(Guid keyId, Guid tenantId, IKDtos.RenameInstallerKeyRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var content = JsonContent.Create(request);
      using var response = await _client.HttpClient.PutAsync($"{HttpConstants.V1.InstallerKeysEndpoint}/{keyId}?tenantId={tenantId}", content, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
