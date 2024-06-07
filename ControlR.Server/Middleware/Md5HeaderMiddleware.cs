using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using System.Security.Cryptography;

namespace ControlR.Server.Middleware;

public class Md5HeaderMiddleware(
    RequestDelegate _next,
    IFileProvider fileProvider,
    IHostApplicationLifetime _appLifetime,
    ILogger<Md5HeaderMiddleware> _logger)
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
            context.Response.Headers.ContentMD5 = cachedHash;
            await _next(context);
            return;
        }

        using var fs = new FileStream(fileInfo.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await MD5.HashDataAsync(fs, _appLifetime.ApplicationStopping);
        var base64Hash = Convert.ToBase64String(hash);
        _memoryCache.Set(filePath, base64Hash, TimeSpan.FromMinutes(10));
        context.Response.Headers.ContentMD5 = base64Hash;
        // TODO: Remove after next release.
        context.Response.Headers["MD5"] = base64Hash;

        await _next(context);
    }
}