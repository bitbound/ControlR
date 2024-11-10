using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using DeviceUpdateRequestDto = ControlR.Libraries.Shared.Dtos.ServerApi.DeviceUpdateRequestDto;

namespace ControlR.Libraries.Shared.Hubs;
public interface IAgentHub
{
    Task SendStreamerDownloadProgress(StreamerDownloadProgressDto progressDto);
    Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
    Task<Result<DeviceUpdateResponseDto>> UpdateDevice(DeviceUpdateRequestDto deviceUpdate);
}
