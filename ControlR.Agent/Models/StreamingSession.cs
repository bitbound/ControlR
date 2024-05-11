using ControlR.Shared.Helpers;
using SimpleIpc;
using System.Diagnostics;

namespace ControlR.Agent.Models;
internal class StreamingSession(Guid sessionId) : IDisposable
{
    public Process? StreamerProcess { get; set; }
    public Guid SessionId { get; } = sessionId;

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
