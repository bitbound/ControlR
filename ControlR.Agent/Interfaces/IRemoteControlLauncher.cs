using ControlR.Shared.Primitives;

namespace ControlR.Agent.Interfaces;
internal interface IRemoteControlLauncher
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
