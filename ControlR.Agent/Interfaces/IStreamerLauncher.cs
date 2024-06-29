using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.Interfaces;
internal interface IStreamerLauncher
{
    Task<Result> CreateSession(
        Guid sessionId,
        Uri websocketUri,
        string viewerConnectionId,
        byte[] authorizedKey,
        int targetWindowsSession = -1,
        bool notifyUserOnSessionStart = false,
        string? viewerName = null);
}
