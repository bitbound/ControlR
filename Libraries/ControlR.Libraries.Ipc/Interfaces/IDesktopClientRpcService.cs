using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Libraries.Ipc.Interfaces;

public interface IDesktopClientRpcService
{
    Task<CheckOsPermissionsResponseIpcDto> CheckOsPermissions(CheckOsPermissionsIpcDto dto);
    Task CloseChatSession(CloseChatSessionIpcDto dto);
    Task<DesktopPreviewResponseIpcDto> GetDesktopPreview(DesktopPreviewRequestIpcDto dto);
    Task InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto);
    Task ReceiveChatMessage(ChatMessageIpcDto dto);
    Task<Result> ReceiveRemoteControlRequest(RemoteControlRequestIpcDto dto);
    Task ShutdownDesktopClient(ShutdownCommandDto dto);
}
