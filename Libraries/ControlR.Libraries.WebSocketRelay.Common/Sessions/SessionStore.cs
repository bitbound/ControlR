using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Libraries.WebSocketRelay.Common.Sessions;

internal interface ISessionStore
{
  int Count { get; }

  void AddOrUpdate(Guid sessionId, SessionSignaler signaler, Func<Guid, SessionSignaler, SessionSignaler> updateFactory);

  bool Exists(Guid sessionId);

  SessionSignaler GetOrAdd(Guid sessionId, Func<Guid, SessionSignaler> createFactory);

  bool TryGet(Guid sessionId, [NotNullWhen(true)] out SessionSignaler? signaler);

  bool TryRemove(Guid sessionId, [NotNullWhen(true)] out SessionSignaler? signaler);
}

internal class SessionStore : ISessionStore
{
  private readonly ConcurrentDictionary<Guid, SessionSignaler> _signalers = new();

  public int Count => _signalers.Count;

  public void AddOrUpdate(Guid sessionId, SessionSignaler signaler, Func<Guid, SessionSignaler, SessionSignaler> updateFactory)
  {
    _signalers.AddOrUpdate(sessionId, signaler, updateFactory);
  }

  public bool Exists(Guid sessionId)
  {
    return _signalers.ContainsKey(sessionId);
  }

  public SessionSignaler GetOrAdd(Guid sessionId, Func<Guid, SessionSignaler> createFactory)
  {
    return _signalers.GetOrAdd(sessionId, createFactory);
  }

  public bool TryGet(Guid sessionId, [NotNullWhen(true)] out SessionSignaler? signaler)
  {
    return _signalers.TryGetValue(sessionId, out signaler);
  }

  public bool TryRemove(Guid sessionId, [NotNullWhen(true)] out SessionSignaler? signaler)
  {
    return _signalers.TryRemove(sessionId, out signaler);
  }
}