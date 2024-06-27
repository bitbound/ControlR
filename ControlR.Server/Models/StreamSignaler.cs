using ControlR.Libraries.Shared.Helpers;
using System.Net.WebSockets;

namespace ControlR.Server.Models;

public class StreamSignaler(Guid sessionId) : IAsyncDisposable
{
    private readonly ManualResetEventAsync _streamerReadySignal = new();
    private readonly ManualResetEventAsync _viewerReadySignal = new();

    private bool _disposedValue;
    public WebSocket? StreamerWebsocket { get; internal set; }
    public bool IsMutallyAcquired => _streamerReadySignal.IsSet && _viewerReadySignal.IsSet;
    public Guid SessionId { get; init; } = sessionId;
    public WebSocket? ViewerWebsocket { get; internal set; }

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
            if (ViewerWebsocket?.State == WebSocketState.Open)
            {
                await ViewerWebsocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Session ended.",
                    cts.Token);
            }
        }
        catch { }

        try
        {
            if (StreamerWebsocket?.State == WebSocketState.Open)
            {
                await StreamerWebsocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Session ended.",
                    cts.Token);
            }
        }
        catch { }

        DisposeHelper.DisposeAll(
                  _streamerReadySignal,
                  StreamerWebsocket,
                  _viewerReadySignal,
                  ViewerWebsocket);

        GC.SuppressFinalize(this);
    }

    public void SignalStreamerReady()
    {
        _streamerReadySignal.Set();
    }

    public void SignalViewerReady()
    {
        _viewerReadySignal.Set();
    }

    public async Task WaitForStreamer(CancellationToken cancellationToken)
    {
        await _streamerReadySignal.Wait(cancellationToken);
    }

    public async Task WaitForViewer(CancellationToken cancellationToken)
    {
        await _viewerReadySignal.Wait(cancellationToken);
    }
}