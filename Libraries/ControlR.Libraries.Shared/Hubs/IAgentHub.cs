using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;

namespace ControlR.Libraries.Shared.Hubs;
public interface IAgentHub
{
    Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
    Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
    Task<Result<DeviceResponseDto>> UpdateDevice(DeviceRequestDto device);
}
