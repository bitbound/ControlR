using ControlR.Server.Services.Interfaces;
using ControlR.Shared.Models;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Server.Services.InMemory;

public class StreamerSessionCacheLocal : IStreamerSessionCache
{
    private static readonly ConcurrentDictionary<Guid, StreamerHubSession> _sessions = new();

    public ConcurrentDictionary<Guid, StreamerHubSession> Sessions => _sessions;

    public void AddOrUpdate(Guid sessionId, StreamerHubSession session)
    {
        _sessions.AddOrUpdate(sessionId, session, (k, v) => session);
    }

    public bool TryGetValue(Guid sessionId, [NotNullWhen(true)] out StreamerHubSession? session)
    {
        return _sessions.TryGetValue(sessionId, out session);
    }

    public bool TryRemove(Guid sessionId, [NotNullWhen(true)] out StreamerHubSession? session)
    {
        return _sessions.TryRemove(sessionId, out session);
    }
}
