using ControlR.Web.Server.Services.Distributed.Locking;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;

namespace ControlR.Web.Server.Services.Distributed;

public class ConnectionCountSynchronizer(
  IConnectionCounter connectionCounter,
  IDistributedLock locker,
  IDistributedCache cache,
  ISystemTime systemTime,
  ILogger<ConnectionCountSynchronizer> logger) : BackgroundService
{
  private readonly double _maxSyncDelayMs = 30_000;
  private readonly double _minSyncDelayMs = 5_000;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      await Task.Delay(GetNextDelay(), stoppingToken);
      await SynchronizeConnectionCounts(
        LockKeys.AgentCount,
        connectionCounter.AgentConnectionLocalCount,
        stoppingToken);

      await SynchronizeConnectionCounts(
        LockKeys.ViewerCount,
        connectionCounter.ViewerConnectionLocalCount,
        stoppingToken);
    }
  }

  private async Task SynchronizeConnectionCounts(string key, int currentCount, CancellationToken cancellationToken)
  {
    await using var lockResult = await locker.TryAcquireLock(key);
    try
    {
      if (!lockResult.LockAcquired)
      {
        return;
      }

      var getResult = await cache.GetOrCreate<Dictionary<Guid, CounterToken>>(key, cancellationToken);

      if (!getResult.IsSuccess)
      {
        logger.LogResult(getResult);

        // If the data is invalid, all we can do is overwrite it.
        if (getResult.HadException && getResult.Exception is MessagePackSerializationException)
        {
          await cache.SetNewValue<Dictionary<Guid, CounterToken>>(key, cancellationToken);
        }

        return;
      }

      var lookup = getResult.Value;

      var expiredClients = lookup.Values
        .Where(x => x.LastUpdated.AddMilliseconds(_maxSyncDelayMs * 2) < systemTime.Now)
        .ToArray();

      foreach (var expiredClient in expiredClients)
      {
        _ = lookup.Remove(expiredClient.NodeId);
      }

      if (!lookup.TryGetValue(locker.NodeId, out var counter))
      {
        counter = new CounterToken { NodeId = locker.NodeId };
        _ = lookup.TryAdd(counter.NodeId, counter);
      }

      counter.Count = currentCount;
      counter.LastUpdated = DateTimeOffset.Now;

      var serialized = MessagePackSerializer.Serialize(lookup, cancellationToken: cancellationToken);
      await cache.SetAsync(key, serialized, cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while synchronizing connection counts.");
    }
  }

  private TimeSpan GetNextDelay()
  {
    var delay = Math.Max(_minSyncDelayMs, Random.Shared.NextDouble() * _maxSyncDelayMs);
    return TimeSpan.FromMilliseconds(delay);
  }
}