using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.LoadTester;
internal class FakeStreamerLauncher : IStreamerLauncher
{
  public Task<Result> CreateSession(Guid sessionId, Uri websocketUri, string viewerConnectionId, int targetWindowsSession = -1, bool notifyUserOnSessionStart = false, string? viewerName = null)
  {
    return Result.Ok().AsTaskResult();
  }
}
