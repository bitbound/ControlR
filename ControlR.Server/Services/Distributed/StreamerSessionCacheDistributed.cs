using ControlR.Server.Services.Interfaces;
using ControlR.Shared.Models;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Server.Services.Distributed;

public class StreamerSessionCacheDistributed : IStreamerSessionCache
{
    public ConcurrentDictionary<Guid, StreamerHubSession> Sessions => throw new NotImplementedException();

    public void AddOrUpdate(Guid sessionId, StreamerHubSession streamerHubSession)
    {
        throw new NotImplementedException();
    }

    public bool TryGetValue(Guid sessionId, [NotNullWhen(true)] out StreamerHubSession? session)
    {
        throw new NotImplementedException();
    }

    public bool TryRemove(Guid sessionId, [NotNullWhen(true)] out StreamerHubSession? session)
    {
        throw new NotImplementedException();
    }
}