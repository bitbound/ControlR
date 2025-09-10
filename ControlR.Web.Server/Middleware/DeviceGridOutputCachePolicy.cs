using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Middleware;

public class DeviceGridOutputCachePolicy : IOutputCachePolicy
{
  private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(30);

  public async ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
  {
    // Only cache if the user is authenticated
    var isAuthenticated = context.HttpContext.User.Identity?.IsAuthenticated == true;
    context.EnableOutputCaching = isAuthenticated;

    if (!isAuthenticated)
    {
      return;
    }

    context.AllowCacheLookup = true;
    context.AllowCacheStorage = true;

    // Set cache duration
    context.ResponseExpirationTimeSpan = _cacheDuration;

    // Vary by user ID
    if (!context.HttpContext.User.TryGetUserId(out var userId))
    {
      userId = Guid.NewGuid();
    }

    // Set user tag for cache eviction
    context.Tags.Add($"user-{userId}");

    // Add tenant-specific tag
    if (context.HttpContext.User.TryGetTenantId(out var tenantId))
    {
      context.Tags.Add($"device-grid-tenant-{tenantId}");
    }
    // Keep the global tag as well if needed
    context.Tags.Add("device-grid");

    // Vary by query parameters and headers
    context.CacheVaryByRules.QueryKeys = "*";
    context.CacheVaryByRules.HeaderNames = new[] { "Authorization" };

    // Vary by request hash - computed from the body
    // Note: ASP.NET Core doesn't natively support varying by POST body, so this is a workaround
    context.HttpContext.Request.EnableBuffering();
    using var reader = new StreamReader(context.HttpContext.Request.Body, leaveOpen: true);
    var requestBody = await reader.ReadToEndAsync(cancellationToken);
    context.HttpContext.Request.Body.Position = 0;

    // Create a hash of the request body to vary by
    var requestHash = ComputeRequestHash(requestBody);
    // Store request hash as a tag to allow for more specific cache invalidation
    context.Tags.Add($"request-{requestHash}");
    context.CacheVaryByRules.VaryByValues.TryAdd(nameof(DeviceSearchRequestDto), requestBody);

    // Add custom response header to indicate cache usage
    context.HttpContext.Response.OnStarting(() =>
    {
      context.HttpContext.Response.Headers["X-DeviceGrid-Cache"] = "true";
      context.HttpContext.Response.Headers["X-DeviceGrid-Cache-Hash"] = requestHash;
      return Task.CompletedTask;
    });
  }

  public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
  {
    context.AllowCacheLookup = true;
    // Use default behavior
    return ValueTask.CompletedTask;
  }

  public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
  {
    context.AllowCacheLookup = true;
    // Use default behavior
    return ValueTask.CompletedTask;
  }

  private static string ComputeRequestHash(string requestBody)
  {
    try
    {
      if (string.IsNullOrEmpty(requestBody))
        return "empty";
      var bytes = System.Text.Encoding.UTF8.GetBytes(requestBody);
      var hash = System.Security.Cryptography.SHA256.HashData(bytes);
      return Convert.ToBase64String(hash)[..10].Replace('/', '_').Replace('+', '-');
    }
    catch
    {
      // If hashing fails, use a random value so it won't match anything else
      return Guid.NewGuid().ToString("N")[..8];
    }
  }
}
