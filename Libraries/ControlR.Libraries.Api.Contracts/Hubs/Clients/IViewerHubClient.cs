using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Dtos.Ui;

namespace ControlR.Libraries.Api.Contracts.Hubs.Clients;

public interface IViewerHubClient
{
  Task InvokeToast(ToastInfo toastInfo);
  Task<bool> ReceiveChatResponse(ChatResponseHubDto dto);
  Task ReceiveDeviceUpdate(DeviceResponseDto deviceDto);
  Task ReceiveServerStats(ServerStatsDto serverStats);
  Task ReceiveTerminalOutput(TerminalOutputDto output);
}