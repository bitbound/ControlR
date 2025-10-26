using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;

namespace ControlR.Libraries.Shared.Hubs.Clients;

public interface IDeviceAccessHubClient : IBrowserHubClientBase
{
  Task<bool> ReceiveChatResponse(ChatResponseHubDto dto);
  Task ReceiveDesktopClientDownloadProgress(DesktopClientDownloadProgressDto progressDto);
  Task ReceiveTerminalOutput(TerminalOutputDto output);
}