using ControlR.Libraries.Shared.Dtos.IpcDtos;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IIpcResponseSender
{
  Task<bool> SendChatResponse(ChatResponseIpcDto response);
}
