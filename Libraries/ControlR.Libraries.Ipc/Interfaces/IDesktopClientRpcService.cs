using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Shared.Primitives;
using StreamJsonRpc;
using PolyType;

namespace ControlR.Libraries.Ipc.Interfaces;

[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IDesktopClientRpcService
{
    Task<CheckOsPermissionsResponseIpcDto> CheckOsPermissions(CheckOsPermissionsIpcDto dto);
    Task CloseChatSession(CloseChatSessionIpcDto dto);
    Task<DesktopPreviewResponseIpcDto> GetDesktopPreview(DesktopPreviewRequestIpcDto dto);
    Task InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto);
    Task ReceiveChatMessage(ChatMessageIpcDto dto);
    Task<Result> ReceiveRemoteControlRequest(RemoteControlRequestIpcDto dto);
    Task ShutdownDesktopClient(ShutdownCommandDto dto);
}
