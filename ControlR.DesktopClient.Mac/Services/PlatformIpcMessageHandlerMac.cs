using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

namespace ControlR.DesktopClient.Mac.Services;

internal class PlatformIpcMessageHandlerMac : IPlatformIpcMessageHandler
{
  public Task<DesktopSessionInfoResponseIpcDto> GetDesktopSessionInfo()
  {
    throw new NotSupportedException("GetDesktopSessionInfo is not supported on macOS.");
  }

  public Task InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto)
  {
    throw new NotSupportedException("Ctrl+Alt+Del is not supported on macOS.");
  }
}
