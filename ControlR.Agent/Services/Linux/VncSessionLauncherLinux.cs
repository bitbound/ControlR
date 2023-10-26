using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services.Linux;

[SupportedOSPlatform("linux")]
internal class VncSessionLauncherLinux : IVncSessionLauncher
{
    public Task<Result<VncSession>> CreateSession(Guid sessionId)
    {
        var session = new VncSession(sessionId);
        return Result.Ok(session).AsTaskResult();
    }
}