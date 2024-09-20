using System.Diagnostics;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Agent.Models;

internal class StreamingSession(string viewerConnectionId) : IDisposable
{
  public string ViewerConnectionId { get; } = viewerConnectionId;
  public Process? StreamerProcess { get; set; }

  public void Dispose()
  {
    try
    {
      StreamerProcess?.Kill();
    }
    catch
    {
    }

    DisposeHelper.DisposeAll(StreamerProcess);
  }
}