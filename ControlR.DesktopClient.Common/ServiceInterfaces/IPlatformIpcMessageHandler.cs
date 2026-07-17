using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IPlatformIpcMessageHandler
{
  Task<DesktopSessionInfoResponseIpcDto> GetDesktopSessionInfo();
  Task InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto);
}
