using ControlR.Agent.Interfaces;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.Services.Fakes;
internal class StreamerLauncherFake : IStreamerLauncher
{
    public Task<Result> CreateSession(
        Guid sessionId, 
        Uri websocketUri,
        string viewerConnectionId,
        int targetWindowsSession = -1, 
        bool notifyUserOnSessionStart = false,
        string? viewerName = null)
    {
        return Result.Fail("Platform not supported.").AsTaskResult();
    }
}
