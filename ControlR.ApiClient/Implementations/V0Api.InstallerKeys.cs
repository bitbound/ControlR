using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V0;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient;

internal partial class V0Api
{
  async Task<ApiResult<V0Dtos.CreateInstallerKeyResponseDto>> IInstallerKeysApi.CreateInstallerKey(V0Dtos.CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.V0.InstallerKeysEndpoint, dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V0Dtos.CreateInstallerKeyResponseDto>(cancellationToken);
    });
  }
}
