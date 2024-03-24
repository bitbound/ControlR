using ControlR.Shared.Dtos;

namespace ControlR.Shared.Hubs;
public interface IAgentHub
{
    Task NotifyViewerDesktopChanged(Guid sessionId, string desktopName);
    Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
    Task UpdateDevice(DeviceDto device);
}
