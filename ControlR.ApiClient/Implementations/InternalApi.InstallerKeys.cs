using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<CreateInstallerKeyResponseDto>> IInstallerKeysApi.CreateInstallerKey(CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.InstallerKeysEndpoint, dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInstallerKeysApi.DeleteInstallerKey(Guid keyId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.Internal.InstallerKeysEndpoint}/{keyId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<AgentInstallerKeyDto[]>> IInstallerKeysApi.GetAllInstallerKeys(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<AgentInstallerKeyDto[]>(HttpConstants.Internal.InstallerKeysEndpoint, cancellationToken));
  }

  async Task<ApiResult<AgentInstallerKeyUsageDto[]>> IInstallerKeysApi.GetInstallerKeyUsages(Guid keyId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<AgentInstallerKeyUsageDto[]>($"{HttpConstants.Internal.InstallerKeysEndpoint}/usages/{keyId}", cancellationToken));
  }

  async Task<ApiResult> IInstallerKeysApi.RenameInstallerKey(RenameInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync($"{HttpConstants.Internal.InstallerKeysEndpoint}/rename", dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}