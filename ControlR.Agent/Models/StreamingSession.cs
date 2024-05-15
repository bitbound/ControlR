using ControlR.Shared.Helpers;
using System.Diagnostics;

namespace ControlR.Agent.Models;
internal class StreamingSession(Guid sessionId, bool lowerUacDuringSession) : IDisposable
{
    public bool LowerUacDuringSession { get; } = lowerUacDuringSession;
    public Guid SessionId { get; } = sessionId;
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
