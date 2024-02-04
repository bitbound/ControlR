using ControlR.Shared.Dtos;

namespace ControlR.Shared.Interfaces.HubClients;

public interface IViewerHubClient : IHubClient
{
    Task ReceiveAgentConnectionCount(int agentConnectionCount);
    Task ReceiveDeviceUpdate(DeviceDto device);

    Task ReceiveTerminalOutput(TerminalOutputDto output);
}