using ControlR.Libraries.Shared.Dtos.IpcDtos;

namespace ControlR.Libraries.Ipc.Interfaces;

public interface IAgentRpcService
{
    Task<bool> SendChatResponse(ChatResponseIpcDto dto);
}
