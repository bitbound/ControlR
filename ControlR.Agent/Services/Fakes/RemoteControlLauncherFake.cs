using ControlR.Agent.Interfaces;
using ControlR.Shared.Extensions;
using ControlR.Shared.Primitives;

namespace ControlR.Agent.Services.Fakes;
internal class RemoteControlLauncherFake : IRemoteControlLauncher
{
    public Task<Result> CreateSession(Guid sessionId, byte[] authorizedKey, int targetWindowsSession = -1, bool notifyUserOnSessionStart = false, string? viewerName = null, Func<double, Task>? onDownloadProgress = null)
    {
        return Result.Fail("Platform not supported.").AsTaskResult();
    }
}
