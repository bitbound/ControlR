using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;

namespace ControlR.Web.Server.Middleware;

[Obsolete("This should be removed in version 0.15.x")]
public class ContentHashHeaderMiddleware(
  RequestDelegate next,
  IFileProvider fileProvider,
  IHostApplicationLifetime appLifetime,
  ILogger<ContentHashHeaderMiddleware> logger)
{
  private static readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IFileProvider _fileProvider = fileProvider;
  private readonly ILogger<ContentHashHeaderMiddleware> _logger = logger;
  private readonly RequestDelegate _next = next;

  public async Task InvokeAsync(HttpContext context)
  {
    if (!HttpMethods.IsHead(context.Request.Method) ||
        !context.Request.Path.StartsWithSegments("/downloads"))
    {
      await _next(context);
      return;
    }

    var filePath = $"wwwroot{context.Request.Path}";
    _logger.LogDebug("Head request started for downloads file.  Path: {FilePath}", filePath);

    var fileInfo = _fileProvider.GetFileInfo(filePath);
    if (!fileInfo.Exists || fileInfo.PhysicalPath is null)
    {
      _logger.LogWarning("File does not exist: {FilePath}", filePath);
      await _next(context);
      return;
    }

    if (_memoryCache.TryGetValue(filePath, out var cachedObject) &&
        cachedObject is string cachedHash)
    {
      _logger.LogDebug("Returning hash from cache.");
      context.Response.Headers["Content-Hash"] = cachedHash;
      await _next(context);
      return;
    }

    _logger.LogDebug("Calculating hash.");
    await using var fs = new FileStream(fileInfo.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    var sha256Hash = await SHA256.HashDataAsync(fs, _appLifetime.ApplicationStopping);
    var hexHash = Convert.ToHexString(sha256Hash);
    context.Response.Headers["Content-Hash"] = hexHash;
    _logger.LogDebug("Hash set in response header.");
    _memoryCache.Set(filePath, hexHash, TimeSpan.FromMinutes(10));

    await _next(context);
  }
}