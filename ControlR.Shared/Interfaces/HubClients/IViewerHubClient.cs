using ControlR.Shared.Dtos;

namespace ControlR.Shared.Interfaces.HubClients;

public interface IViewerHubClient : IHubClient
{
    Task ReceiveDeviceUpdate(DeviceDto device);

    Task ReceiveTerminalOutput(TerminalOutputDto output);
}