using ControlR.Libraries.Shared.Helpers;
using System.Diagnostics;

namespace ControlR.Agent.Models;
internal class StreamingSession(string _viewerConnectionId, bool _lowerUacDuringSession) : IDisposable
{
    public bool LowerUacDuringSession { get; } = _lowerUacDuringSession;
    public string ViewerConnectionId { get; } = _viewerConnectionId;
    public Process? StreamerProcess { get; set; }
    public void Dispose()
    {
        try
        {
            StreamerProcess?.Kill();
        }
        catch { }
        DisposeHelper.DisposeAll(StreamerProcess);
    }
}
