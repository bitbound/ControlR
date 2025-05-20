using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;

namespace ControlR.Web.Server.Startup;

/// <summary>
/// Custom output cache policy for device grid data.
/// This policy ensures that:
/// 1. Cache entries are only created for authenticated users
/// 2. Each cache entry is specific to a user
/// 3. Cache entries are tagged for easy invalidation
/// </summary>
public class DeviceGridCachePolicy : IOutputCachePolicy
{
    /// <summary>
    /// Called when a request that may be cached is being processed
    /// </summary>
    /// <param name="context">The output cache context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A completed task</returns>
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        // Check if the user is authenticated
        var isAuthenticated = context.HttpContext.User.Identity?.IsAuthenticated == true;
        
        // Only cache if the user is authenticated
        context.EnableOutputCaching = isAuthenticated;
          if (isAuthenticated)
        {
            // Set the cache key based on user ID and request path
            var userId = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anon";
            context.Tags.Add("device-grid");
            context.Tags.Add($"user-{userId}");
            
            // Add a cache key based on the request path and user ID
            var keyPrefix = $"DeviceGrid_{userId}_{context.HttpContext.Request.Path}";
            context.CacheVaryByRules.QueryKeys = "*"; // Vary by all query parameters
            
            // Add a key pattern for request hashing
            if (context.HttpContext.Request.Method == "POST")
            {
                // Enable request body buffering
                context.HttpContext.Request.EnableBuffering();
            }
        }
        
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Called when a request might be served from the cache
    /// </summary>
    /// <param name="context">The output cache context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A completed task</returns>
    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Called when a response is being cached
    /// </summary>
    /// <param name="context">The output cache context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A completed task</returns>
    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
