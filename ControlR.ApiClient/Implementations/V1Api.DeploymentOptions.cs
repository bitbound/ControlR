using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using DODtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeploymentOptions;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<DODtos.DeploymentOptionsDto>> IDeploymentOptionsApi.GetDeploymentOptions(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.V1.DeploymentOptionsEndpoint}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DODtos.DeploymentOptionsDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DODtos.DeploymentTagCapabilityResponseDto>> IDeploymentOptionsApi.GetTagCapability(
    Guid tenantId,
    DODtos.DeploymentTagCapabilityRequestDto request,
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
      return await response.Content.ReadFromJsonAsync<DODtos.DeploymentTagCapabilityResponseDto>(cancellationToken);
    });
  }
}