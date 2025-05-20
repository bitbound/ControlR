using ControlR.Libraries.Shared.Dtos.ServerApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services;

/// <summary>
/// Legacy device grid caching service implementation.
/// This is being phased out in favor of ASP.NET Core's built-in output caching.
/// </summary>
public class DeviceGridCacheService : IDeviceGridCacheService
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(2);
    private const string CacheKeyPrefix = "DeviceGrid_";
    private readonly ILogger<DeviceGridCacheService> _logger;

    public DeviceGridCacheService(IMemoryCache cache, ILogger<DeviceGridCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public ActionResult<DeviceGridResponseDto>? GetFromCache(DeviceGridRequestDto request, string userId)
    {
        var cacheKey = GenerateCacheKey(request, userId);
        var result = _cache.TryGetValue(cacheKey, out DeviceGridResponseDto? cachedResponse) 
            ? cachedResponse 
            : null;
            
        if (result != null)
        {
            _logger.LogDebug("Cache hit for user {UserId} with request {Request}", userId, cacheKey);
            return new ActionResult<DeviceGridResponseDto>(result);
        }
        
        return null;
    }

    public void SetCache(DeviceGridRequestDto request, DeviceGridResponseDto response, string userId)
    {
        var cacheKey = GenerateCacheKey(request, userId);
        _cache.Set(cacheKey, response, _cacheDuration);
        _logger.LogDebug("Cached response for user {UserId} with {ItemCount} items", userId, response.Items.Count);
    }

    public void InvalidateUserCache(string userId)
    {
        _logger.LogInformation("Request to invalidate cache for user {UserId} - using expiration", userId);
        // This is a simplified implementation
        // In a real-world scenario with a distributed cache,
        // we would need a more sophisticated approach to pattern-based cache invalidation
    }

    public void InvalidateAllCache()
    {
        _logger.LogInformation("Invalidating all device grid cache");
        // In a memory cache implementation, we cannot easily remove all entries with a prefix
        // For production, consider using a more sophisticated caching solution
        if (_cache is MemoryCache memoryCache)
        {
            // Clear the entire cache - not ideal but effective
            memoryCache.Compact(1.0);
        }
    }

    private string GenerateCacheKey(DeviceGridRequestDto request, string userId)
    {
        // Create a deterministic cache key based on the request properties
        var key = $"{CacheKeyPrefix}{userId}_";
        
        // Add basic filtering parameters
        key += $"search={request.SearchText ?? string.Empty}";
        key += $"_hideOffline={request.HideOfflineDevices}";
        key += $"_page={request.Page}";
        key += $"_pageSize={request.PageSize}";
        
        // Add tags
        if (request.TagIds != null && request.TagIds.Count > 0)
        {
            key += $"_tags={string.Join(",", request.TagIds.OrderBy(t => t))}";
        }
        
        // Add sort parameters
        if (request.SortDefinitions != null && request.SortDefinitions.Count > 0)
        {
            var sortKey = string.Join(";", request.SortDefinitions
                .OrderBy(s => s.SortOrder)
                .Select(s => $"{s.PropertyName}_{s.Descending}_{s.SortOrder}"));
            key += $"_sort={sortKey}";
        }
        
        return key;
    }
}
