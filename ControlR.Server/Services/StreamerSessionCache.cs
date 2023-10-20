using ControlR.Shared.Models;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Server.Services;

public interface IStreamerSessionCache
{
    ConcurrentDictionary<Guid, StreamerHubSession> Sessions { get; }
    void AddOrUpdate(Guid sessionId, StreamerHubSession streamerHubSession);

    bool TryGetValue(Guid sessionId, [NotNullWhen(true)] out StreamerHubSession? session);
    bool TryRemove(Guid sessionId, [NotNullWhen(true)] out StreamerHubSession? session);
}

public class StreamerSessionCache : IStreamerSessionCache
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
