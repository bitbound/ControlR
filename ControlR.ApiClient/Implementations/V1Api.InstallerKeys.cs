using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<CreateInstallerKeyResponseDto>> IInstallerKeysApi.CreateInstallerKey(CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.V1.InstallerKeysEndpoint, dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>(cancellationToken);
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

  async Task<ApiResult<InstallerKeysResponseDto>> IInstallerKeysApi.GetAllInstallerKeys(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.V1.InstallerKeysEndpoint}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InstallerKeysResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<InstallerKeyUsagesResponseDto>> IInstallerKeysApi.GetInstallerKeyUsages(Guid keyId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.V1.InstallerKeysEndpoint}/{keyId}/usages?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InstallerKeyUsagesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInstallerKeysApi.RenameInstallerKey(Guid keyId, Guid tenantId, RenameInstallerKeyRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var content = JsonContent.Create(request);
      using var response = await _client.HttpClient.PutAsync($"{HttpConstants.V1.InstallerKeysEndpoint}/{keyId}?tenantId={tenantId}", content, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
