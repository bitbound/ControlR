using ControlR.Shared.Helpers;
using ControlR.Shared.Primitives;
using System.Net.WebSockets;

namespace ControlR.Server.Models;

public class StreamSignaler(Guid sessionId) : IAsyncDisposable
{
    private readonly ManualResetEventAsync _agentReadySignal = new();
    private readonly ManualResetEventAsync _viewerReadySignal = new();

    private bool _disposedValue;
    public WebSocket? AgentVncWebsocket { get; internal set; }
    public bool IsMutallyAcquired => _agentReadySignal.IsSet && _viewerReadySignal.IsSet;
    public Guid SessionId { get; init; } = sessionId;
    public WebSocket? ViewerVncWebsocket { get; internal set; }

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
            if (ViewerVncWebsocket?.State == WebSocketState.Open)
            {
                await ViewerVncWebsocket.CloseAsync(
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
                  _agentReadySignal,
                  AgentVncWebsocket,
                  _viewerReadySignal,
                  ViewerVncWebsocket);

        GC.SuppressFinalize(this);
    }

    public void SignalAgentReady()
    {
        _agentReadySignal.Set();
    }

    public void SignalViewerReady()
    {
        _viewerReadySignal.Set();
    }

    public async Task WaitForAgent(CancellationToken cancellationToken)
    {
        await _agentReadySignal.Wait(cancellationToken);
    }

    public async Task WaitForViewer(CancellationToken cancellationToken)
    {
        await _viewerReadySignal.Wait(cancellationToken);
    }
}