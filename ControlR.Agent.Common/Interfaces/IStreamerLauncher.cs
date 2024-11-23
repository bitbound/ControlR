using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.Common.Interfaces;
internal interface IStreamerLauncher
{
  Task<Result> CreateSession(
      Guid sessionId,
      Uri websocketUri,
      string viewerConnectionId,
      int targetWindowsSession = -1,
      bool notifyUserOnSessionStart = false,
      string? viewerName = null);
}
