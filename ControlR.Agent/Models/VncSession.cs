using System.Diagnostics;

namespace ControlR.Agent.Models;

internal class VncSession(Guid sessionId) : IDisposable
{
    public Guid SessionId { get; } = sessionId;
    public Process? VncProcess { get; set; }

    public void Dispose()
    {
        try
        {
            VncProcess?.Kill();
        }
        catch { }
        try
        {
            VncProcess?.Dispose();
        }
        catch { }
    }
}