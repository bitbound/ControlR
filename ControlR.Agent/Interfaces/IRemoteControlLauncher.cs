using ControlR.Shared.Primitives;

namespace ControlR.Agent.Interfaces;
internal interface IRemoteControlLauncher
{
    Task<Result> CreateSession(
     Guid sessionId,
     byte[] authorizedKey,
     int targetWindowsSession = -1,
     bool notifyUserOnSessionStart = false,
     string? viewerName = null,
     Func<double, Task>? onDownloadProgress = null);
}
