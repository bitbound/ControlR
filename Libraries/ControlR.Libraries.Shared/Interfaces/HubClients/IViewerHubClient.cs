using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IViewerHubClient : IHubClient
{
  Task InvokeToast(ToastInfo toastInfo);
  Task ReceiveDeviceUpdate(DeviceDto deviceDto);
  Task ReceiveServerStats(ServerStatsDto serverStats);
  Task ReceiveDesktopClientDownloadProgress(DesktopClientDownloadProgressDto progressDto);
  Task ReceiveTerminalOutput(TerminalOutputDto output);
  Task<bool> ReceiveChatResponse(ChatResponseHubDto dto);
}