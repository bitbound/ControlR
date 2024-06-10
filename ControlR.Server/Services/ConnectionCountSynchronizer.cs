
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Services;
using ControlR.Server.Extensions;
using ControlR.Server.Models;
using ControlR.Server.Services.Distributed.Locking;
using ControlR.Server.Services.Interfaces;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace ControlR.Server.Services;

public class ConnectionCountSynchronizer(
    IConnectionCounter _connectionCounter,
    IDistributedLock _locker,
    IDistributedCache _cache,
    ISystemTime _systemTime,
    ILogger<ConnectionCountSynchronizer> _logger) : BackgroundService
{
    private readonly double _minSyncDelayMs = 5_000;
    private readonly double _maxSyncDelayMs = 30_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(GetNextDelay(), stoppingToken);
            await SynchronizeConnectionCounts(
                LockKeys.AgentCount, 
                _connectionCounter.AgentConnectionLocalCount,
                stoppingToken);

            await SynchronizeConnectionCounts(
                LockKeys.ViewerCount, 
                _connectionCounter.ViewerConnectionLocalCount,
                stoppingToken);
        }
    }

    private async Task SynchronizeConnectionCounts(string key, int currentCount, CancellationToken cancellationToken)
    {
        try
        {
            var lockResult = await _locker.TryAcquireLock(key);
            if (!lockResult.LockAcquired)
            {
                return;
            }

            var getResult = await _cache.GetOrCreate<Dictionary<Guid, ConnectionCounter>>(key, cancellationToken);

            if (!getResult.IsSuccess)
            {
                _logger.LogResult(getResult);

                // If the data is invalid, all we can do is overwrite it;
                if (getResult.HadException && getResult.Exception is MessagePackSerializationException)
                {
                    await _cache.SetNewValue<Dictionary<Guid, ConnectionCounter>>(key, cancellationToken);
                }
                return;
            }

            var lookup = getResult.Value;

            var expiredClients = lookup.Values
                .Where(x => x.LastUpdated.AddMilliseconds(_maxSyncDelayMs * 2) < _systemTime.Now)
                .ToArray();

            foreach (var expiredClient in expiredClients)
            {
                _ = lookup.Remove(expiredClient.NodeId);
            }

            if (!lookup.TryGetValue(_locker.NodeId, out var counter))
            {
                counter = new() { NodeId = _locker.NodeId };
                _ = lookup.TryAdd(counter.NodeId, counter);
            }

            counter.Count = currentCount;
            counter.LastUpdated = DateTimeOffset.Now;

            var serialized = MessagePackSerializer.Serialize(lookup, cancellationToken: cancellationToken);
            await _cache.SetAsync(key, serialized, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while synchronizing connection counts.");
        }
    }

    private TimeSpan GetNextDelay()
    {
        var delay = Math.Max(_minSyncDelayMs, Random.Shared.NextDouble() * _maxSyncDelayMs);
        return TimeSpan.FromMilliseconds(delay);
    }
}
