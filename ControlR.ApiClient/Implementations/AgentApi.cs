using ControlR.ApiClient.Interfaces.Agent;

namespace ControlR.ApiClient;

internal partial class AgentApi(ControlrApi client) :
  IControlrAgentApi,
  IAgentDeploymentApi,
  IAgentDevicesApi,
  IAgentUpdateApi
{
  private readonly ControlrApi _client = client;

  public IAgentDeploymentApi Deployment => this;
  public IAgentDevicesApi Devices => this;
  public IAgentUpdateApi Updates => this;
}
