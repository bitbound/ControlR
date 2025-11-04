using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Hubs.Clients;

public interface IViewerHubClient
{
  Task InvokeToast(ToastInfo toastInfo);
  Task<bool> ReceiveChatResponse(ChatResponseHubDto dto);
  Task ReceiveDeviceUpdate(DeviceDto deviceDto);
  Task ReceiveServerStats(ServerStatsDto serverStats);
  Task ReceiveTerminalOutput(TerminalOutputDto output);
}