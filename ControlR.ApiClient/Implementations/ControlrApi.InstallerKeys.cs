using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<CreateInstallerKeyResponseDto>> IInstallerKeysApi.CreateInstallerKey(CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.Internal.InstallerKeysEndpoint, dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInstallerKeysApi.DeleteInstallerKey(Guid keyId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.Internal.InstallerKeysEndpoint}/{keyId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<AgentInstallerKeyDto[]>> IInstallerKeysApi.GetAllInstallerKeys(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<AgentInstallerKeyDto[]>(HttpConstants.Internal.InstallerKeysEndpoint, cancellationToken));
  }

  async Task<ApiResult<AgentInstallerKeyUsageDto[]>> IInstallerKeysApi.GetInstallerKeyUsages(Guid keyId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<AgentInstallerKeyUsageDto[]>($"{HttpConstants.Internal.InstallerKeysEndpoint}/usages/{keyId}", cancellationToken));
  }

  async Task<ApiResult> IInstallerKeysApi.IncrementInstallerKeyUsage(Guid keyId, Guid? deviceId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var url = deviceId.HasValue
        ? $"public/installer-keys/increment-usage/{keyId}?deviceId={deviceId.Value}"
        : $"public/installer-keys/increment-usage/{keyId}";

      using var response = await _client.PostAsync(url, null, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<CreateInstallerKeyResponseDto>> IInstallerKeysApi.IssueInstallerKey(IssueInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.V1.InstallerKeysEndpoint, dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInstallerKeysApi.RenameInstallerKey(RenameInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PutAsJsonAsync($"{HttpConstants.Internal.InstallerKeysEndpoint}/rename", dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
