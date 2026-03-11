using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Common.Services.Windows.Internals;

internal sealed class DesktopClientLaunchState(
  int sessionId,
  int processId,
  IProcess process,
  DateTimeOffset startedAt) : IDisposable
{
  public IProcess Process { get; } = process;
  public int ProcessId { get; } = processId;
  public int SessionId { get; } = sessionId;
  public DateTimeOffset StartedAt { get; } = startedAt;

  public void Dispose()
  {
    Disposer.DisposeAll(Process);
  }
}