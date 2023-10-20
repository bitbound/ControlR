using ControlR.Agent.Interfaces;
using ControlR.Shared;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services.Linux;

[SupportedOSPlatform("linux")]
internal class RemoteControlLauncherLinux : IRemoteControlLauncher
{
    public Task<Result> CreateSession(
        Guid sessionId, 
        byte[] authorizedKey, 
        int targetWindowsSession = -1, 
        string targetDesktop = "", 
        Func<double, Task>? onDownloadProgress = null)
    {
        throw new NotImplementedException();
    }
}
