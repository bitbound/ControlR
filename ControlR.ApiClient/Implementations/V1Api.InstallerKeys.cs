using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<CreateInstallerKeyResponseDto>> IV1InstallerKeysApi.CreateInstallerKey(IssueInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.V1.InstallerKeysEndpoint, dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>(cancellationToken);
    });
  }
}