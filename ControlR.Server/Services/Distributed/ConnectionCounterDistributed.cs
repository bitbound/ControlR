using ControlR.Server.Models;
using ControlR.Server.Services.Distributed.Locking;
using ControlR.Server.Services.Interfaces;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;

namespace ControlR.Server.Services.Distributed;

public class ConnectionCounterDistributed(
    IDistributedCache _cache,
    ILogger<ConnectionCounterDistributed> _logger) : IConnectionCounter
{
    private volatile int _agentCount;

    private volatile int _streamerCount;
    private volatile int _viewerCount;
    public int AgentConnectionLocalCount => _agentCount;

    public int StreamerConnectionLocalCount => _streamerCount;

    public int ViewerConnectionLocalCount => _viewerCount;

    public void DecrementAgentCount()
    {
        Interlocked.Decrement(ref _agentCount);
    }

    public void DecrementViewerCount()
    {
        Interlocked.Decrement(ref _viewerCount);
    }

    public void DecrementStreamerCount()
    {
        Interlocked.Decrement(ref _streamerCount);
    }

    public async Task<Result<int>> GetAgentConnectionCount()
    {
        return await GetCount(LockKeys.AgentCount);
    }

    public async Task<Result<int>> GetStreamerConnectionCount()
    {
        return await GetCount(LockKeys.StreamerCount);
    }

    public async Task<Result<int>> GetViewerConnectionCount()
    {
        return await GetCount(LockKeys.ViewerCount);
    }
    public void IncrementAgentCount()
    {
        Interlocked.Increment(ref _agentCount);
    }

    public void IncrementStreamerCount()
    {
        Interlocked.Increment(ref _streamerCount);
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


            var deserialized = MessagePackSerializer.Deserialize<Dictionary<Guid, CounterToken>>(cachedValue) ??
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
