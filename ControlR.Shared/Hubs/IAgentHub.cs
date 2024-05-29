using ControlR.Shared.Dtos;
using ControlR.Shared.Models;

namespace ControlR.Shared.Hubs;
public interface IAgentHub
{
    Task<bool> GetGitHubIntegrationEnabled();

    Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
    Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
    Task UpdateDevice(DeviceDto device);
}
