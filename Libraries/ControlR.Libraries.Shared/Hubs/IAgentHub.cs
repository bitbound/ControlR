using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Hubs;
public interface IAgentHub
{
    Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
    Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
    Task UpdateDevice(DeviceDto device);
}
