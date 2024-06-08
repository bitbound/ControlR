using ControlR.Libraries.Shared.Primitives;
using ControlR.Server.Models;
using ControlR.Server.Services.Distributed.Locking;
using ControlR.Server.Services.Interfaces;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;

namespace ControlR.Server.Services.Distributed;

// TODO: This needs a different strategy.  If a node goes down,
// the disconnect events won't fire, and the backplane's count
// will be incorrect indefinitely.  Nodes might need to track
// their own totals in memory and sync with the backplane.
public class ConnectionCounterDistributed(
    IDistributedCache _cache,
    ILogger<ConnectionCounterDistributed> _logger) : IConnectionCounter
{
    private volatile int _agentCount;

    private volatile int _viewerCount;

    public int AgentConnectionLocalCount => _agentCount;

    public int ViewerConnectionLocalCount => _viewerCount;

    public void DecrementAgentCount()
    {
        Interlocked.Decrement(ref _agentCount);
    }

    public void DecrementViewerCount()
    {
        Interlocked.Decrement(ref _viewerCount);
    }

    public async Task<Result<int>> GetAgentConnectionCount()
    {
        return await GetCount(LockKeys.AgentCount);
    }

    public async Task<Result<int>> GetViewerConnectionCount()
    {
        return await GetCount(LockKeys.ViewerCount);
    }

    public void IncrementAgentCount()
    {
        Interlocked.Increment(ref _agentCount);
    }

    public void IncrementViewerCount()
    {
        Interlocked.Increment(ref _viewerCount);
    }

    private async Task<Result<int>> GetCount(string key)
    {
        try
        {
            var cachedValue = await _cache.GetAsync(key);
            if (cachedValue is null)
            {
                return Result.Ok(0);
            }


            var deserialized = MessagePackSerializer.Deserialize<Dictionary<Guid, ConnectionCounter>>(cachedValue) ??
                throw new MessagePackSerializationException("Failed to deserialize cached value.");
            
            if (deserialized.Count == 0)
            {
                return Result.Ok(0);
            }

            var sum = deserialized.Values.Sum(x => x.Count);
            return Result.Ok(sum);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to increment or decrement counter key {KeyName}.", key);
            return Result.Fail<int>(ex);
        }
    }
}
