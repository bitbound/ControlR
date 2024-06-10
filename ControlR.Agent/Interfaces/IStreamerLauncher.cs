using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.Interfaces;
internal interface IStreamerLauncher
{
    Task<Result> CreateSession(
        Guid sessionId,
        string viewerConnectionId,
        byte[] authorizedKey,
        int targetWindowsSession = -1,
        bool notifyUserOnSessionStart = false,
        bool lowerUacDuringSession = false,
        string? viewerName = null);
}
