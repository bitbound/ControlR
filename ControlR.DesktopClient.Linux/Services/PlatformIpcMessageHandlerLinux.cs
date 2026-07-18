using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

namespace ControlR.DesktopClient.Linux.Services;

internal class PlatformIpcMessageHandlerLinux : IPlatformIpcMessageHandler
{
  public Task<DesktopSessionInfoResponseIpcDto> GetDesktopSessionInfo()
  {
    throw new NotSupportedException("GetDesktopSessionInfo is not supported on Linux.");
  }

  public Task InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto)
  {
    throw new NotSupportedException("Ctrl+Alt+Del is not supported on Linux.");
  }
}
