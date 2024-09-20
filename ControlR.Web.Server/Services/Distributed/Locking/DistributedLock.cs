using StackExchange.Redis;

namespace ControlR.Web.Server.Services.Distributed.Locking;

public interface IDistributedLock
{
  Guid NodeId { get; }
  Task<LockToken> TryAcquireLock(string key);
  Task<LockToken> TryAcquireLock(string key, TimeSpan timeout);
  Task<LockToken> TryAcquireLock(string key, TimeSpan timeout, TimeSpan lockExpiration);
}

public class DistributedLock(
  IConnectionMultiplexer multi,
  ILogger<AlertStoreDistributed> logger) : IDistributedLock
{
  public Guid NodeId { get; } = Guid.NewGuid();

  public Task<LockToken> TryAcquireLock(string key)
  {
    return TryAcquireLock(key, TimeSpan.FromSeconds(10));
  }

  public Task<LockToken> TryAcquireLock(string key, TimeSpan timeout)
  {
    return TryAcquireLock(key, timeout, timeout);
  }

  public async Task<LockToken> TryAcquireLock(string key, TimeSpan timeout, TimeSpan lockExpiration)
  {
    var lockKey = new RedisKey(key);
    var lockValue = new RedisValue(Guid.NewGuid().ToString());

    try
    {
      var db = multi.GetDatabase();
      using var cts = new CancellationTokenSource(timeout);
      using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

      do
      {
        if (await db.LockTakeAsync(lockKey, lockValue, lockExpiration))
        {
          return LockToken.Succeeded(lockKey, lockValue, logger,
            async () => { await db.LockReleaseAsync(lockKey, lockValue); });
        }
      } while (await timer.WaitForNextTickAsync(cts.Token));

      logger.LogCritical("Failed to acquire distributed lock.  Key: {LockKey}", key);
    }
    catch (OperationCanceledException ex)
    {
      logger.LogCritical(ex, "Timed out while trying to acquire distributed lock.  Key: {LockKey}", key);
    }
    catch (Exception ex)
    {
      logger.LogCritical(ex, "Error while trying to acquire distributed lock.  Key: {LockKey}", key);
    }

    return LockToken.Failed(lockKey);
  }
}