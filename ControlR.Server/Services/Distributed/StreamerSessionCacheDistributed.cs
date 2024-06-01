using ControlR.Server.Services.Interfaces;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Server.Services.Distributed;

public class StreamerSessionCacheDistributed(
    IDistributedCache _distributedCache): IStreamerSessionCache
{
    public Task<Result> AddOrUpdate(Guid sessionId, StreamerHubSession streamerHubSession)
    {
        
        throw new NotImplementedException();
    }

    public Task<Result<StreamerHubSession[]>> GetAllSessions()
    {
        throw new NotImplementedException();
    }

    public Task<Result<StreamerHubSession>> TryGetValue(Guid sessionId)
    {
        throw new NotImplementedException();
    }

    public Task<Result<StreamerHubSession>> TryRemove(Guid sessionId)
    {
        throw new NotImplementedException();
    }
}