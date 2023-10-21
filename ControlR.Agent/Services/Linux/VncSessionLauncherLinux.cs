using ControlR.Agent.Interfaces;
using ControlR.Shared;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services.Linux;

[SupportedOSPlatform("linux")]
internal class VncSessionLauncherLinux : IVncSessionLauncher
{
    public Task<Result<Process>> CreateSession(Guid sessionId, string password, Func<double, Task>? onDownloadProgress = null)
    {
        throw new NotImplementedException();
    }
}