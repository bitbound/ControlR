using ControlR.ApiClient.Interfaces.Agent;

namespace ControlR.ApiClient;

internal partial class AgentApi(ControlrApi client) :
  IControlrAgentApi,
  IAgentDevicesApi,
  IAgentUpdateApi
{
  private readonly ControlrApi _client = client;

  public IAgentDevicesApi Devices => this;
  public IAgentUpdateApi Updates => this;
}
