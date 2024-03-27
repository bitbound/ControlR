using ControlR.Shared.Dtos;

namespace ControlR.Shared.Hubs;
public interface IAgentHub
{
    Task NotifyViewerDesktopChanged(Guid sessionId, string desktopName);
    Task SendRemoteControlDownloadProgress(Guid streamingSessionId, string viewerConnectionId, double downloadProgress);

    Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
    Task UpdateDevice(DeviceDto device);
}
