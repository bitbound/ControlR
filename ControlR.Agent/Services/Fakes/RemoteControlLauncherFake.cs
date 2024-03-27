using ControlR.Agent.Interfaces;
using ControlR.Shared.Extensions;
using ControlR.Shared.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlR.Agent.Services.Fakes;
internal class RemoteControlLauncherFake : IRemoteControlLauncher
{
    public Task<Result> CreateSession(Guid sessionId, byte[] authorizedKey, int targetWindowsSession = -1, string targetDesktop = "", Func<double, Task>? onDownloadProgress = null)
    {
        return Result.Fail("Platform not supported.").AsTaskResult();
    }
}
