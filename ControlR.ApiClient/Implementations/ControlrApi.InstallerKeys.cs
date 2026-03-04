using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<CreateInstallerKeyResponseDto>> IInstallerKeysApi.CreateInstallerKey(CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.InstallerKeysEndpoint, dto, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInstallerKeysApi.DeleteInstallerKey(Guid keyId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.InstallerKeysEndpoint}/{keyId}", cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult<AgentInstallerKeyDto[]>> IInstallerKeysApi.GetAllInstallerKeys(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<AgentInstallerKeyDto[]>(HttpConstants.InstallerKeysEndpoint, cancellationToken));
  }

  async Task<ApiResult<AgentInstallerKeyUsageDto[]>> IInstallerKeysApi.GetInstallerKeyUsages(Guid keyId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<AgentInstallerKeyUsageDto[]>($"{HttpConstants.InstallerKeysEndpoint}/usages/{keyId}", cancellationToken));
  }

  async Task<ApiResult> IInstallerKeysApi.IncrementInstallerKeyUsage(Guid keyId, Guid? deviceId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var url = deviceId.HasValue
        ? $"{HttpConstants.InstallerKeysEndpoint}/increment-usage/{keyId}?deviceId={deviceId.Value}"
        : $"{HttpConstants.InstallerKeysEndpoint}/increment-usage/{keyId}";

      using var response = await _client.PostAsync(url, null, cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult> IInstallerKeysApi.RenameInstallerKey(RenameInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PutAsJsonAsync($"{HttpConstants.InstallerKeysEndpoint}/rename", dto, cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }
}
