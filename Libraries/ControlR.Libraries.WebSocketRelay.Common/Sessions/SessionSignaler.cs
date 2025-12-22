using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace ControlR.Libraries.WebSocketRelay.Common.Sessions;

internal class SessionSignaler : IAsyncDisposable
{
  private readonly string _accessToken;
  private readonly TaskCompletionSource _requesterSignaled = new();
  private readonly TaskCompletionSource _requesterWebsocketSet = new();
  private readonly TaskCompletionSource _responderSignaled = new();
  private readonly TaskCompletionSource _responderWebsocketSet = new();
  private readonly ConcurrentDictionary<RelayRole, Guid> _roleMap = new();
  private readonly Task[] _signalTasks;

  private bool _disposedValue;
  private WebSocket? _requesterWebsocket;
  private WebSocket? _responderWebsocket;

  public SessionSignaler(string accessToken)
  {
    _accessToken = accessToken;
    _signalTasks = [_requesterSignaled.Task, _responderSignaled.Task];
  }

  public WebSocket? RequesterWebsocket
  {
    get => _requesterWebsocket;
    internal set
    {
      _requesterWebsocket = value;
      _requesterWebsocketSet.TrySetResult();
    }
  }
  public WebSocket? ResponderWebsocket
  {
    get => _responderWebsocket;
    internal set
    {
      _responderWebsocket = value;
      _responderWebsocketSet.TrySetResult();
    }
  }

  public async ValueTask DisposeAsync()
  {
    if (_disposedValue)
    {
      return;
    }

    _disposedValue = true;

    _ = _requesterSignaled.TrySetResult();
    _ = _responderSignaled.TrySetResult();

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    try
    {
      if (ResponderWebsocket?.State == WebSocketState.Open)
      {
        await ResponderWebsocket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Session ended.",
            cts.Token);
      }
    }
    catch { }

    try
    {
      if (RequesterWebsocket?.State == WebSocketState.Open)
      {
        await RequesterWebsocket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Session ended.",
            cts.Token);
      }
    }
    catch { }

    Disposer.DisposeAll(
      RequesterWebsocket,
      ResponderWebsocket);

    GC.SuppressFinalize(this);
  }

  public WebSocket GetCallerWebsocket(Guid callerPeerId)
  {
    if (_roleMap.TryGetValue(RelayRole.Requester, out var requesterId) && callerPeerId == requesterId)
    {
      return RequesterWebsocket ?? throw new InvalidOperationException("Requester websocket not found.");
    }

    if (_roleMap.TryGetValue(RelayRole.Responder, out var responderId) && callerPeerId == responderId)
    {
      return ResponderWebsocket ?? throw new InvalidOperationException("Responder websocket not found.");
    }

    throw new InvalidOperationException("Caller peer ID not found in session.");
  }

  public WebSocket GetPartnerWebsocket(Guid callerPeerId)
  {
    if (_roleMap.TryGetValue(RelayRole.Requester, out var requesterId) && callerPeerId == requesterId)
    {
      return ResponderWebsocket ?? throw new InvalidOperationException("Responder websocket not found.");
    }

    if (_roleMap.TryGetValue(RelayRole.Responder, out var responderId) && callerPeerId == responderId)
    {
      return RequesterWebsocket ?? throw new InvalidOperationException("Requester websocket not found.");
    }

    throw new InvalidOperationException("Caller peer ID not found in session.");
  }

  public async Task SetWebsocket(WebSocket websocket, Guid callerPeerId, CancellationToken cancellationToken)
  {
    if (_roleMap.TryGetValue(RelayRole.Requester, out var requesterId) && callerPeerId == requesterId)
    {
      RequesterWebsocket = websocket;
    }
    else if (_roleMap.TryGetValue(RelayRole.Responder, out var responderId) && callerPeerId == responderId)
    {
      ResponderWebsocket = websocket;
    }
    else
    {
      throw new InvalidOperationException("Caller peer ID not found in session.");
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
    await Task
        .WhenAll(_requesterWebsocketSet.Task, _responderWebsocketSet.Task)
        .WaitAsync(linkedCts.Token);
  }

  public bool SignalReady(RelayRole role)
  {
    return role switch
    {
      RelayRole.Requester => _requesterSignaled.TrySetResult(),
      RelayRole.Responder => _responderSignaled.TrySetResult(),
      _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown relay role."),
    };
  }

  public bool TryAssignRole(Guid peerId, RelayRole role)
  {
    return _roleMap.TryAdd(role, peerId);
  }

  public bool ValidateToken(string accessToken)
  {
    return accessToken == _accessToken;
  }

  public async Task WaitForPartner(CancellationToken cancellationToken)
  {
    await Task.WhenAll(_signalTasks).WaitAsync(cancellationToken);
  }
}
