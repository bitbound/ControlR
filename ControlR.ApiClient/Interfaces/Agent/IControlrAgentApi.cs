namespace ControlR.ApiClient.Interfaces.Agent;

public interface IControlrAgentApi
{
  IAgentDeploymentApi Deployment { get; }
  IAgentDevicesApi Devices { get; }
  IAgentUpdateApi Updates { get; }
}
