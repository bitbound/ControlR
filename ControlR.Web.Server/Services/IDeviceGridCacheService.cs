using ControlR.Libraries.Shared.Dtos.ServerApi;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Services;

/// <summary>
/// Interface for device grid caching service.
/// Note: This interface is being phased out in favor of ASP.NET Core's built-in output caching.
/// </summary>
public interface IDeviceGridCacheService
{
    /// <summary>
    /// Gets cached device grid response based on request parameters and user ID
    /// </summary>
    ActionResult<DeviceSearchResponseDto>? GetFromCache(DeviceSearchRequestDto request, string userId);
    
    /// <summary>
    /// Caches device grid response for a specific user
    /// </summary>
    void SetCache(DeviceSearchRequestDto request, DeviceSearchResponseDto response, string userId);
    
    /// <summary>
    /// Invalidates cache for a specific user
    /// </summary>
    void InvalidateUserCache(string userId);
    
    /// <summary>
    /// Invalidates cache for all users
    /// </summary>
    void InvalidateAllCache();
}
