using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using DODtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeploymentOptions;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IDeploymentOptionsApi
{
  [ApiRoute($"{HttpConstants.V1.DeploymentOptionsEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<DODtos.DeploymentOptionsDto>> GetDeploymentOptions(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeploymentOptionsEndpoint}/tag-capability?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DODtos.DeploymentTagCapabilityResponseDto>> GetTagCapability(
    Guid tenantId,
    DODtos.DeploymentTagCapabilityRequestDto request,
    CancellationToken cancellationToken = default);
}