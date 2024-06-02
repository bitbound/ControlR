using ControlR.Server.Services.Distributed.Locking;
using ControlR.Server.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace ControlR.Server.Services.Distributed;

// TODO: This needs a different strategy.  If a node goes down,
// the disconnect events won't fire, and the backplane's count
// will be incorrect indefinitely.  Nodes might need to track
// their own totals in memory and sync with the backplane.
public class ConnectionCounterDistributed(
    IDistributedCache _cache,
    IDistributedLock _locker,
    ILogger<ConnectionCounterDistributed> _logger) : IConnectionCounter
{
    public async Task DecrementAgentCount()
    {
        await AdjustCount(LockKeys.AgentCount, -1);
    }

    public async Task DecrementViewerCount()
    {
        await AdjustCount(LockKeys.ViewerCount, -1);
    }

    public async Task<Result<int>> GetAgentConnectionCount()
    {
        return await GetCount(LockKeys.AgentCount);
    }

    public async Task<Result<int>> GetViewerConnectionCount()
    {
        return await GetCount(LockKeys.ViewerCount);
    }

    public async Task IncrementAgentCount()
    {
        await AdjustCount(LockKeys.AgentCount, 1);
    }

    public async Task IncrementViewerCount()
    {
        await AdjustCount(LockKeys.ViewerCount, 1);
    }

    private async Task AdjustCount(string key, int adjustBy)
    {
        try
        {
            await using var result = await _locker.TryAcquireLock(key);
            if (!result.LockAcquired)
            {
                return;
            }

            var current = await _cache.GetAsync(key);
            if (current is null)
            {
                return;
            }

            var count = BitConverter.ToInt32(current) + adjustBy;
            await _cache.SetAsync(key, BitConverter.GetBytes(count));
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to increment or decrement counter key {KeyName}.", key);
        }
    }
    private async Task<Result<int>> GetCount(string key)
    {
        try
        {
            var current = await _cache.GetAsync(key);
            if (current is null)
            {
                return Result.Ok(0);
            }

            var count = BitConverter.ToInt32(current);
            return Result.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to increment or decrement counter key {KeyName}.", key);
            return Result.Fail<int>(ex);
        }
    }
}
