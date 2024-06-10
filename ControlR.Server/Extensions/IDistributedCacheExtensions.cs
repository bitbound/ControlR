using ControlR.Libraries.Shared.Primitives;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;

namespace ControlR.Server.Extensions;

public static class IDistributedCacheExtensions
{
    public static async Task<Result<T>> GetOrCreate<T>(
        this IDistributedCache cache,
        string cacheKey,
        CancellationToken cancellationToken = default)
        where T : new()
    {
        try
        {
            var cacheValue = await cache.GetAsync(cacheKey, cancellationToken);
            if (cacheValue is not byte[] bytes)
            {
                var newValue = new T();
                var newCacheValue = MessagePackSerializer.Serialize(newValue, cancellationToken: cancellationToken);
                await cache.SetAsync(
                    cacheKey,
                    newCacheValue,
                    cancellationToken);

                return Result.Ok(newValue);
            }

            var result = MessagePackSerializer.Deserialize<T>(bytes, cancellationToken: cancellationToken)
                ?? throw new MessagePackSerializationException("Failed to deserialize cache value.");
            
            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            return Result.Fail<T>(ex);
        }
    }

    public static async Task SetNewValue<T>(
     this IDistributedCache cache,
     string cacheKey,
     CancellationToken cancellationToken = default)
     where T : new()
    {
        var value = new T();
        await cache.SetAsync(
            cacheKey, 
            MessagePackSerializer.Serialize(value, cancellationToken: cancellationToken), 
            cancellationToken);
    }
}
