using ControlR.Server.Models;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Server.Services;

public interface ISessionStore
{
    int Count { get; }

    void AddOrUpdate(Guid sessionId, SessionSignaler signaler, Func<Guid, SessionSignaler, SessionSignaler> updateFactory);

    bool Exists(Guid sessionId);

    SessionSignaler GetOrAdd(Guid sessionId, Func<Guid, SessionSignaler> createFactory);

    bool TryGet(Guid sessionId, [NotNullWhen(true)] out SessionSignaler? signaler);

    bool TryRemove(Guid sessionId, [NotNullWhen(true)] out SessionSignaler? signaler);
}

public class SessionStore(ILogger<SessionStore> logger) : ISessionStore
{
    private readonly ILogger<SessionStore> _logger = logger;
    private readonly ConcurrentDictionary<Guid, SessionSignaler> _proxyStreams = new();

    public int Count => _proxyStreams.Count;

    public void AddOrUpdate(Guid sessionId, SessionSignaler signaler, Func<Guid, SessionSignaler, SessionSignaler> updateFactory)
    {
        _proxyStreams.AddOrUpdate(sessionId, signaler, updateFactory);
    }

    public bool Exists(Guid sessionId)
    {
        return _proxyStreams.ContainsKey(sessionId);
    }

    public SessionSignaler GetOrAdd(Guid sessionId, Func<Guid, SessionSignaler> createFactory)
    {
        return _proxyStreams.GetOrAdd(sessionId, createFactory);
    }

    public bool TryGet(Guid sessionId, [NotNullWhen(true)] out SessionSignaler? signaler)
    {
        return _proxyStreams.TryGetValue(sessionId, out signaler);
    }

    public bool TryRemove(Guid sessionId, [NotNullWhen(true)] out SessionSignaler? signaler)
    {
        return _proxyStreams.TryRemove(sessionId, out signaler);
    }
}