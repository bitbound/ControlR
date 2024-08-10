using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using System.Security.Cryptography;

namespace ControlR.Server.Middleware;

public class ContentHashHeaderMiddleware(
    RequestDelegate _next,
    IFileProvider fileProvider,
    IHostApplicationLifetime _appLifetime,
    ILogger<ContentHashHeaderMiddleware> _logger)
{
    private static readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Head ||
            !context.Request.Path.StartsWithSegments("/downloads"))
        {
            await _next(context);
            return;
        }

        var filePath = $"wwwroot{context.Request.Path}";
        _logger.LogDebug("Head request started for downloads file.  Path: {FilePath}", filePath);

        var fileInfo = fileProvider.GetFileInfo(filePath);
        if (!fileInfo.Exists || fileInfo.PhysicalPath is null)
        {
            _logger.LogDebug("File does not exist: {FilePath}", filePath);
            await _next(context);
            return;
        }

        if (_memoryCache.TryGetValue(filePath, out var cachedObject) &&
            cachedObject is string cachedHash)
        {
            context.Response.Headers["Content-Hash"] = cachedHash;
            await _next(context);
            return;
        }

        using var fs = new FileStream(fileInfo.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var sha256Hash = await SHA256.HashDataAsync(fs, _appLifetime.ApplicationStopping);
        var hexHash = Convert.ToHexString(sha256Hash);
        context.Response.Headers["Content-Hash"] = hexHash;

        _memoryCache.Set(filePath, hexHash, TimeSpan.FromMinutes(10));

        await _next(context);
    }
}