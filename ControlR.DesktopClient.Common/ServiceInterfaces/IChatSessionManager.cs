using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IChatSessionManager
{
  Task AddMessage(Guid sessionId, ChatMessageIpcDto message);
  Task CloseChatSession(Guid sessionId, bool notifyUser);
  bool IsSessionActive(Guid sessionId);
  Task<bool> SendResponse(Guid sessionId, string message);
}
