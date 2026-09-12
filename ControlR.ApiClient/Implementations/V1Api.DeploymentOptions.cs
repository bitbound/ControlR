using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeploymentOptions;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<DeploymentOptionsDto>> IDeploymentOptionsApi.GetDeploymentOptions(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.V1.DeploymentOptionsEndpoint}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeploymentOptionsDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DeploymentTagCapabilityResponseDto>> IDeploymentOptionsApi.GetTagCapability(
    Guid tenantId,
    DeploymentTagCapabilityRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var content = JsonContent.Create(request);
      using var response = await _client.HttpClient.PostAsync(
        $"{HttpConstants.V1.DeploymentOptionsEndpoint}/tag-capability?tenantId={tenantId}",
        content,
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeploymentTagCapabilityResponseDto>(cancellationToken);
    });
  }
}