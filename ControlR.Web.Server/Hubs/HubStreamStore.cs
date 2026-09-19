using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Hubs;

public interface IHubStreamStore
{
  HubStreamSignaler<T> GetOrCreate<T>(Guid streamId, TimeSpan expiration);
  bool TryGet<T>(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler<T>? signaler);
  bool TryRemove<T>(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler<T>? signaler);
}

public class HubStreamStore(ILogger<HubStreamStore> logger, IMemoryCache memoryCache) : IHubStreamStore
{
  private readonly ILogger<HubStreamStore> _logger = logger;
  private readonly IMemoryCache _memoryCache = memoryCache;

  public HubStreamSignaler<T> GetOrCreate<T>(Guid streamId, TimeSpan expiration)
  {
    if (_memoryCache.TryGetValue(streamId, out var existing))
    {
      if (existing is HubStreamSignaler<T> typedExisting)
      {
        return typedExisting;
      }

      _logger.LogWarning("Stream session {StreamId} requested with mismatched type. Existing: {ExistingType}, Requested: {RequestedType}",
        streamId, existing?.GetType().FullName, typeof(T).FullName);

      _memoryCache.Remove(streamId);

      if (existing is IDisposable disposable)
      {
        disposable.Dispose();
      }
    }

    var cacheEntryOptions = new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = expiration,
      PostEvictionCallbacks = { new PostEvictionCallbackRegistration { EvictionCallback = OnEviction } }
    };

    var signaler = new HubStreamSignaler<T>(streamId, () => TryRemove<T>(streamId, out _));
    return _memoryCache.Set(streamId, signaler, cacheEntryOptions);
  }

  public bool TryGet<T>(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler<T>? signaler)
  {
    if (_memoryCache.TryGetValue(streamId, out var value) && value is HubStreamSignaler<T> typed)
    {
      signaler = typed;
      return true;
    }
    signaler = null;
    return false;
  }

  public bool TryRemove<T>(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler<T>? signaler)
  {
    if (_memoryCache.TryGetValue(streamId, out var value) && value is HubStreamSignaler<T> cachedSignaler)
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
    if (value is IDisposable disposable)
    {
      _logger.LogDebug("Stream session {StreamId} evicted from cache. Reason: {Reason}", key, reason);
      disposable.Dispose();
    }
  }
}
