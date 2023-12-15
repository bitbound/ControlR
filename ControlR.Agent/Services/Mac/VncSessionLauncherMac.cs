using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Services;
using ControlR.Shared.Extensions;
using ControlR.Shared.Helpers;
using ControlR.Shared.Primitives;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services.Mac;

[SupportedOSPlatform("macos")]
internal class VncSessionLauncherMac() : IVncSessionLauncher
{
    public Task CleanupSessions()
    {
        return Task.CompletedTask;
    }

    public Task<Result<VncSession>> CreateSession(Guid sessionId, string password)
    {
        var session = new VncSession(sessionId, false);
        return Result.Ok(session).AsTaskResult();
    }
}