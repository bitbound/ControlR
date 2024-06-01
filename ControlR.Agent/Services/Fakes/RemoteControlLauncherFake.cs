using ControlR.Agent.Interfaces;
using ControlR.Shared.Extensions;
using ControlR.Shared.Primitives;

namespace ControlR.Agent.Services.Fakes;
internal class RemoteControlLauncherFake : IRemoteControlLauncher
{
    public Task<Result> CreateSession(
        Guid sessionId, 
        string viewerConnectionId,
        byte[] authorizedKey, 
        int targetWindowsSession = -1, 
        bool notifyUserOnSessionStart = false,
        bool lowerUacDuringSession = false,
        string? viewerName = null)
    {
        return Result.Fail("Platform not supported.").AsTaskResult();
    }
}
