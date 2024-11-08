using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;

namespace ControlR.Web.Server.Middleware;

public class ContentHashHeaderMiddleware(
  RequestDelegate next,
  IFileProvider fileProvider,
  IHostApplicationLifetime appLifetime,
  ILogger<ContentHashHeaderMiddleware> logger)
{
  private static readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

  public async Task InvokeAsync(HttpContext context)
  {
    if (context.Request.Method != HttpMethods.Head ||
        !context.Request.Path.StartsWithSegments("/downloads"))
    {
      await next(context);
      return;
    }

    var filePath = $"wwwroot{context.Request.Path}";
    logger.LogDebug("Head request started for downloads file.  Path: {FilePath}", filePath);

    var fileInfo = fileProvider.GetFileInfo(filePath);
    if (!fileInfo.Exists || fileInfo.PhysicalPath is null)
    {
      logger.LogDebug("File does not exist: {FilePath}", filePath);
      await next(context);
      return;
    }

    if (_memoryCache.TryGetValue(filePath, out var cachedObject) &&
        cachedObject is string cachedHash)
    {
      context.Response.Headers["Content-Hash"] = cachedHash;
      await next(context);
      return;
    }

    await using var fs = new FileStream(fileInfo.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    var sha256Hash = await SHA256.HashDataAsync(fs, appLifetime.ApplicationStopping);
    var hexHash = Convert.ToHexString(sha256Hash);
    context.Response.Headers["Content-Hash"] = hexHash;

    _memoryCache.Set(filePath, hexHash, TimeSpan.FromMinutes(10));

    await next(context);
  }
}