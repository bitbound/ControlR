using ControlR.Shared.Helpers;
using System.Net.WebSockets;

namespace ControlR.Server.Models;

public class StreamSignaler(Guid sessionId) : IDisposable
{
    private bool _disposedValue;

    public SemaphoreSlim AgentVncReady { get; } = new(0, 1);
    public WebSocket? AgentVncWebsocket { get; internal set; }
    public SemaphoreSlim NoVncViewerReady { get; } = new(0, 1);
    public WebSocket? NoVncWebsocket { get; internal set; }
    public Guid SessionId { get; init; } = sessionId;

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                DisposeHelper.DisposeAll(
                    AgentVncReady,
                    AgentVncWebsocket,
                    NoVncViewerReady,
                    NoVncWebsocket);
            }

            _disposedValue = true;
        }
    }
}