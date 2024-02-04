using ControlR.Shared.Dtos;

namespace ControlR.Shared.Hubs;
public interface IAgentHub
{
    Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
    Task UpdateDevice(DeviceDto device);
}
