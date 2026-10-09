using ControlR.ApiClient.Interfaces.Agent;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;

namespace ControlR.ApiClient;

internal partial class AgentApi
{
  async Task<ApiResult<AgentDeploymentOptionsDto>> IAgentDeploymentApi.GetDeploymentOptions(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<AgentDeploymentOptionsDto>(
        HttpConstants.Agent.DeploymentOptionsEndpoint,
        cancellationToken));
  }
}
