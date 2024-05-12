using ControlR.Shared.Dtos;

namespace ControlR.Shared.Hubs;
public interface IAgentHub
{
    Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);

    Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
    Task UpdateDevice(DeviceDto device);
}
