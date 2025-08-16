using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using DeviceDto = ControlR.Libraries.Shared.Dtos.ServerApi.DeviceDto;

namespace ControlR.Libraries.Shared.Hubs;
public interface IAgentHub
{
  Task SendDesktopClientDownloadProgress(DesktopClientDownloadProgressDto progressDto);
  Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
  Task<Result<DeviceDto>> UpdateDevice(DeviceDto deviceDto);
}
