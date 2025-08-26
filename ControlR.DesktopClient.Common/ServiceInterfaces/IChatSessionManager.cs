using ControlR.Libraries.Shared.Dtos.IpcDtos;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IChatSessionManager
{
  Task<Guid> CreateChatSession(Guid sessionId, int targetSystemSession, int targetProcessId, string viewerConnectionId);
  Task AddMessage(Guid sessionId, ChatMessageIpcDto message);
  Task<bool> SendResponse(Guid sessionId, string message);
  Task CloseChatSession(Guid sessionId);
  bool IsSessionActive(Guid sessionId);
}
