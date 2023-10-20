using ControlR.Shared.Helpers;
using SimpleIpc;
using System.Diagnostics;

namespace ControlR.Agent.Models;
internal class StreamingSession(Guid sessionId, byte[] authorizedKey, int targetWindowsSession, string targetDesktop) : IDisposable
{
    public Process? StreamerProcess { get; set; }
    public Guid SessionId { get; } = sessionId;
    public int TargetWindowsSession { get; } = targetWindowsSession;
    public byte[] AuthorizedKey { get; } = authorizedKey;
    public Process? WatcherProcess { get; set; }
    public string LastDesktop { get; set; } = targetDesktop;
    public IIpcServer? IpcServer { get; set; }
    public string AgentPipeName { get; } = Guid.NewGuid().ToString();

    public void Dispose()
    {
        try
        {
            StreamerProcess?.Kill();
        }
        catch { }
        try
        {
            WatcherProcess?.Kill();
        }
        catch { }
        DisposeHelper.DisposeAll(IpcServer, StreamerProcess, WatcherProcess);
    }
}
