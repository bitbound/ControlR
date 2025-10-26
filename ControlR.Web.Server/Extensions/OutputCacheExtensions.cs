using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Extensions;

/// <summary>
/// Extension methods for output cache operations
/// </summary>
public static class OutputCacheExtensions
{

    /// <summary>
    /// Invalidates the device grid cache for a specific device
    /// </summary>
    /// <param name="outputCacheStore">The output cache store</param>
    /// <param name="deviceId">The device ID to invalidate cache for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static ValueTask InvalidateDeviceCacheAsync(
        this IOutputCacheStore outputCacheStore,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        return outputCacheStore.EvictByTagAsync($"device-{deviceId}", cancellationToken);
    }

    /// <summary>
    /// Invalidates the device grid cache for all tenants
    /// </summary>
    /// <param name="outputCacheStore"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask InvalidateDeviceGridCacheAsync(
        this IOutputCacheStore outputCacheStore,
        CancellationToken cancellationToken = default)
    {
        return outputCacheStore.EvictByTagAsync("device-grid", cancellationToken);
    }

    /// <summary>
    /// Invalidates the device grid cache for a specific tenant
    /// </summary>
    /// <param name="outputCacheStore"></param>
    /// <param name="tenantId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask InvalidateDeviceGridCacheForTenantAsync(
        this IOutputCacheStore outputCacheStore,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return outputCacheStore.EvictByTagAsync($"device-grid-tenant-{tenantId}", cancellationToken);
    }

    /// <summary>
    /// Invalidates cache for a specific device grid request based on hash
    /// </summary>
    /// <param name="outputCacheStore">The output cache store</param>
    /// <param name="requestHash">The hash of the request to invalidate cache for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static ValueTask InvalidateDeviceGridRequestCacheAsync(
        this IOutputCacheStore outputCacheStore,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        return outputCacheStore.EvictByTagAsync($"request-{requestHash}", cancellationToken);
    }

    /// <summary>
    /// Invalidates the device grid cache for a specific user
    /// </summary>
    /// <param name="outputCacheStore">The output cache store</param>
    /// <param name="userId">The ID of the user to invalidate cache for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static ValueTask InvalidateUserDeviceGridCacheAsync(
        this IOutputCacheStore outputCacheStore,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return outputCacheStore.EvictByTagAsync($"user-{userId}", cancellationToken);
    }
}
