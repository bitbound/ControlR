using ControlR.Shared.Primitives;
using StackExchange.Redis;

namespace ControlR.Server.Services.Distributed.Locking;

public interface IDistributedLock
{
    Task<LockToken> TryAcquireLock(string key);
    Task<LockToken> TryAcquireLock(string key, TimeSpan timeout);
}

public class DistributedLock(
    IConnectionMultiplexer _multi,
    ILogger<AlertStoreDistributed> _logger) : IDistributedLock
{
    public async Task<LockToken> TryAcquireLock(string key)
    {
        return await TryAcquireLock(key, TimeSpan.MaxValue);
    }
    public async Task<LockToken> TryAcquireLock(string key, TimeSpan timeout)
    {
        var lockKey = new RedisKey(key);
        var lockValue = new RedisValue(Guid.NewGuid().ToString());

        try
        {
            var db = _multi.GetDatabase();

            using var cts = new CancellationTokenSource(timeout);
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

            do
            {
                if (await db.LockTakeAsync(lockKey, lockValue, TimeSpan.MaxValue))
                {
                    return LockToken.Succeeded(lockKey, lockValue, _logger, async () =>
                    {
                        await db.LockReleaseAsync(lockKey, lockValue);
                    });
                }
            }
            while (await timer.WaitForNextTickAsync(cts.Token));

            _logger.LogError("Failed to acquire distributed lock.  Key: {LockKey}", key);
            return LockToken.Failed(lockKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while trying to acquire distributed lock.  Key: {LockKey}", key);
            return LockToken.Failed(lockKey);
        }

    }
}
