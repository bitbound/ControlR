using ControlR.Server.Services.Interfaces;
using ControlR.Shared.Extensions;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
using System.Collections.Concurrent;

namespace ControlR.Server.Services.InMemory;

public class StreamerSessionCacheLocal : IStreamerSessionCache
{
    private static readonly ConcurrentDictionary<Guid, StreamerHubSession> _sessions = new();

    public ConcurrentDictionary<Guid, StreamerHubSession> Sessions => _sessions;

    public Task<Result> AddOrUpdate(Guid sessionId, StreamerHubSession streamerHubSession)
    {
        _sessions.AddOrUpdate(sessionId, streamerHubSession, (k, v) =>
        {
            v.StreamerConnectionId ??= streamerHubSession.StreamerConnectionId;
            v.AgentConnectionId ??= streamerHubSession.AgentConnectionId;
            v.ViewerConnectionId ??= streamerHubSession.ViewerConnectionId;
            v.Displays = streamerHubSession.Displays;
            return v;
        });
        return Result.Ok().AsTaskResult();
    }

    public Task<Result<StreamerHubSession[]>> GetAllSessions()
    {
        return Result.Ok(_sessions.Values.ToArray()).AsTaskResult();
    }
    public Task<Result<StreamerHubSession>> TryGetValue(Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var streamerHubSession))
        {
            return Result.Ok(streamerHubSession).AsTaskResult();
        }
        return Result.Fail<StreamerHubSession>("Session not found.").AsTaskResult();
    }

    public Task<Result<StreamerHubSession>> TryRemove(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var streamerHubSession))
        {
            return Result.Ok(streamerHubSession).AsTaskResult();
        }
        return Result.Fail<StreamerHubSession>("Session not found.").AsTaskResult();
    }
}
