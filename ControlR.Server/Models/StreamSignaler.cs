using ControlR.Shared.Helpers;
using System.Net.WebSockets;

namespace ControlR.Server.Models;

public class StreamSignaler(Guid sessionId) : IAsyncDisposable
{
    private bool _disposedValue;

    public SemaphoreSlim AgentVncReady { get; } = new(0, 1);
    public WebSocket? AgentVncWebsocket { get; internal set; }
    public SemaphoreSlim NoVncViewerReady { get; } = new(0, 1);
    public WebSocket? NoVncWebsocket { get; internal set; }
    public Guid SessionId { get; init; } = sessionId;

    public async ValueTask DisposeAsync()
    {
        if (_disposedValue)
        {
            return;
        }

        _disposedValue = true;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            if (NoVncWebsocket?.State == WebSocketState.Open)
            {
                await NoVncWebsocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Session ended.",
                    cts.Token);
            }
        }
        catch { }

        try
        {
            if (AgentVncWebsocket?.State == WebSocketState.Open)
            {
                await AgentVncWebsocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Session ended.",
                    cts.Token);
            }
        }
        catch { }

        DisposeHelper.DisposeAll(
                  AgentVncReady,
                  AgentVncWebsocket,
                  NoVncViewerReady,
                  NoVncWebsocket);

        GC.SuppressFinalize(this);
    }
}