using Microsoft.Extensions.Logging;

namespace ControlR.Web.Server.Services;

/// <summary>
/// Provides specialized logging for device grid caching operations
/// </summary>
public class DeviceGridCachingLogger
{
    private readonly ILogger<DeviceGridCachingLogger> _logger;

    public DeviceGridCachingLogger(ILogger<DeviceGridCachingLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Log cache hit event
    /// </summary>
    /// <param name="userId">The user ID accessing the cache</param>
    /// <param name="requestInfo">Basic information about the request</param>
    public void LogCacheHit(string userId, string requestInfo)
    {
        _logger.LogInformation(
            "Cache HIT: User {UserId} requested {RequestInfo}", 
            userId, 
            requestInfo);
    }

    /// <summary>
    /// Log cache miss event
    /// </summary>
    /// <param name="userId">The user ID accessing the cache</param>
    /// <param name="requestInfo">Basic information about the request</param>
    public void LogCacheMiss(string userId, string requestInfo)
    {
        _logger.LogInformation(
            "Cache MISS: User {UserId} requested {RequestInfo}", 
            userId, 
            requestInfo);
    }

    /// <summary>
    /// Log cache invalidation event
    /// </summary>
    /// <param name="reason">The reason for cache invalidation</param>
    /// <param name="tag">The cache tag being invalidated</param>
    public void LogCacheInvalidation(string reason, string tag)
    {
        _logger.LogInformation(
            "Cache INVALIDATED: {Reason} for tag {Tag}", 
            reason, 
            tag);
    }

    /// <summary>
    /// Log cache storage event
    /// </summary>
    /// <param name="userId">The user ID whose response is being cached</param>
    /// <param name="itemCount">The number of items in the cached response</param>
    /// <param name="totalCount">The total number of items available</param>
    public void LogCacheStorage(string userId, int itemCount, int totalCount)
    {
        _logger.LogDebug(
            "Cache STORED: User {UserId} response with {ItemCount} items (of {TotalCount} total)", 
            userId, 
            itemCount, 
            totalCount);
    }
    
    /// <summary>
    /// Log cache error event
    /// </summary>
    /// <param name="operation">The operation that caused the error</param>
    /// <param name="exception">The exception that occurred</param>
    public void LogCacheError(string operation, Exception exception)
    {
        _logger.LogError(
            exception,
            "Cache ERROR during {Operation}: {ErrorMessage}", 
            operation, 
            exception.Message);
    }

    /// <summary>
    /// Log cache hit with request details
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="requestHash">The hash of the request</param>
    /// <param name="requestDetails">Details about the request</param>
    public void LogCacheHitWithDetails(string userId, string requestHash, string requestDetails)
    {
        _logger.LogInformation(
            "Cache HIT: User {UserId} with request hash {RequestHash} - {RequestDetails}", 
            userId, 
            requestHash,
            requestDetails);
    }
    
    /// <summary>
    /// Log cache invalidation event with detailed reason
    /// </summary>
    /// <param name="reason">The reason for cache invalidation</param>
    /// <param name="tag">The cache tag being invalidated</param>
    /// <param name="details">Additional details about what triggered the invalidation</param>
    public void LogCacheInvalidationWithDetails(string reason, string tag, string details)
    {
        _logger.LogInformation(
            "Cache INVALIDATED: {Reason} for tag {Tag} - Details: {Details}", 
            reason, 
            tag,
            details);
    }
}
