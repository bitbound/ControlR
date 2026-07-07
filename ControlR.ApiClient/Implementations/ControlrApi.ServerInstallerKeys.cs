using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<CreateInstallerKeyResponseDto>> IServerInstallerKeysApi.Create(ServerCreateInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.InstallerKeysEndpoint}/server", dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IServerInstallerKeysApi.Delete(Guid id, Guid tenantId, Guid userId, bool isTenantAdmin, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.InstallerKeysEndpoint}/server/{id}?tenantId={tenantId}&userId={userId}&isTenantAdmin={isTenantAdmin}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<AgentInstallerKeyDto[]>> IServerInstallerKeysApi.GetAll(Guid tenantId, Guid userId, bool isTenantAdmin, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<AgentInstallerKeyDto[]>($"{HttpConstants.InstallerKeysEndpoint}/server?tenantId={tenantId}&userId={userId}&isTenantAdmin={isTenantAdmin}", cancellationToken));
  }

  async Task<ApiResult<AgentInstallerKeyUsageDto[]>> IServerInstallerKeysApi.GetUsages(Guid keyId, Guid tenantId, Guid userId, bool isTenantAdmin, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<AgentInstallerKeyUsageDto[]>($"{HttpConstants.InstallerKeysEndpoint}/server/usages/{keyId}?tenantId={tenantId}&userId={userId}&isTenantAdmin={isTenantAdmin}", cancellationToken));
  }

  async Task<ApiResult> IServerInstallerKeysApi.Rename(ServerRenameInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PutAsJsonAsync($"{HttpConstants.InstallerKeysEndpoint}/server/rename", dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
