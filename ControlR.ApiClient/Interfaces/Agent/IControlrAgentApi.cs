namespace ControlR.ApiClient.Interfaces.Agent;

public interface IControlrAgentApi
{
  IAgentDevicesApi Devices { get; }
  IAgentUpdateApi Updates { get; }
}
