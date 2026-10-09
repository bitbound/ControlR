using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;

namespace ControlR.ApiClient.Interfaces.Agent;

public interface IAgentDeploymentApi
{
  [ApiRoute($"{HttpConstants.Agent.DeploymentOptionsEndpoint}", "GET")]
  Task<ApiResult<AgentDeploymentOptionsDto>> GetDeploymentOptions(CancellationToken cancellationToken = default);
}
