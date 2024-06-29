using ControlR.Libraries.Shared.Helpers;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace ControlR.Server.Models;
public class SessionSignaler : IAsyncDisposable
{
    private readonly Guid _creatorRequestId;
    private readonly ConcurrentQueue<TaskCompletionSource> _signalQueue = new();
    private readonly Task[] _signalTasks;
    private readonly TaskCompletionSource _websocket1ValueSet = new();
    private readonly TaskCompletionSource _websocket2ValueSet = new();
    private bool _disposedValue;
    private WebSocket? _websocket1;
    private WebSocket? _websocket2;

    public SessionSignaler(Guid requestId)
    {
        _creatorRequestId = requestId;
        _signalQueue.Enqueue(new TaskCompletionSource());
        _signalQueue.Enqueue(new TaskCompletionSource());
        _signalTasks = _signalQueue.Select(x => x.Task).ToArray();
    }

    public WebSocket? Websocket1
    {
        get => _websocket1;
        internal set
        {
            _websocket1 = value;
            _websocket1ValueSet.TrySetResult();
        }
    }

    public WebSocket? Websocket2
    {
        get => _websocket2;
        internal set
        {
            _websocket2 = value;
            _websocket2ValueSet.TrySetResult();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposedValue)
        {
            return;
        }

        _disposedValue = true;

        while (_signalQueue.TryDequeue(out var signaler))
        {
            _ = signaler.TrySetResult();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            if (Websocket2?.State == WebSocketState.Open)
            {
                await Websocket2.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Session ended.",
                    cts.Token);
            }
        }
        catch { }

        try
        {
            if (Websocket1?.State == WebSocketState.Open)
            {
                await Websocket1.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Session ended.",
                    cts.Token);
            }
        }
        catch { }

        DisposeHelper.DisposeAll(
                  Websocket1,
                  Websocket2);

        GC.SuppressFinalize(this);
    }

    public WebSocket GetCallerWebsocket(Guid callerRequestId)
    {
        if (callerRequestId == _creatorRequestId)
        {
            return Websocket1 ?? throw new InvalidOperationException("Websocket1 not found.");
        }
        else
        {
            return Websocket2 ?? throw new InvalidOperationException("Websocket2 not found.");
        }
    }

    public WebSocket GetPartnerWebsocket(Guid callerRequestId)
    {
        if (callerRequestId == _creatorRequestId)
        {
            return Websocket2 ?? throw new InvalidOperationException("Websocket2 not found.");
        }
        else
        {
            return Websocket1 ?? throw new InvalidOperationException("Websocket1 not found.");
        }
    }
    public async Task SetWebsocket(WebSocket websocket, Guid callerRequestId, CancellationToken cancellationToken)
    {
        if (callerRequestId == _creatorRequestId)
        {
            Websocket1 = websocket;
        }
        else
        {
            Websocket2 = websocket;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
        await Task
            .WhenAll(_websocket1ValueSet.Task, _websocket2ValueSet.Task)
            .WaitAsync(linkedCts.Token);
    }

    public bool SignalReady()
    {
        if (!_signalQueue.TryDequeue(out var tcs))
        {
            return false;
        }

        return tcs.TrySetResult();
    }

    public async Task WaitForPartner(CancellationToken cancellationToken)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
        await Task.WhenAll(_signalTasks).WaitAsync(linkedCts.Token);
    }
}