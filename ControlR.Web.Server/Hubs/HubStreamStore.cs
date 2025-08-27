using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Hubs;

public interface IHubStreamStore
{
  HubStreamSignaler GetOrCreate(Guid streamId, TimeSpan expiration);

  bool TryGet(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler? signaler);
  bool TryRemove(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler? signaler);
}

public class HubStreamStore(ILogger<HubStreamStore> logger, IMemoryCache memoryCache) : IHubStreamStore
{
  private readonly IMemoryCache _memoryCache = memoryCache;
  private readonly ILogger<HubStreamStore> _logger = logger;

  public HubStreamSignaler GetOrCreate(Guid streamId, TimeSpan expiration)
  {
    if (_memoryCache.TryGetValue(streamId, out var existing) && existing is HubStreamSignaler signaler)
    {
      return signaler;
    }

    var cacheEntryOptions = new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = expiration,
      PostEvictionCallbacks = { new PostEvictionCallbackRegistration { EvictionCallback = OnEviction } }
    };

    signaler = new HubStreamSignaler(streamId, () => TryRemove(streamId, out _));
    return _memoryCache.Set(streamId, signaler, cacheEntryOptions);
  }

  public bool TryGet(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler? signaler)
  {
    return _memoryCache.TryGetValue(streamId, out signaler);
  }

  public bool TryRemove(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler? signaler)
  {
    if (_memoryCache.TryGetValue(streamId, out var value) && value is HubStreamSignaler cachedSignaler)
    {
      _memoryCache.Remove(streamId);
      signaler = cachedSignaler;
      return true;
    }
    signaler = null;
    return false;
  }

  private void OnEviction(object key, object? value, EvictionReason reason, object? state)
  {
    if (value is HubStreamSignaler signaler)
    {
      _logger.LogDebug("Stream session {StreamId} evicted from cache. Reason: {Reason}", key, reason);
      signaler.Dispose();
    }
  }
}
