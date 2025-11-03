using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using ControlR.Libraries.Shared.Constants;
using Microsoft.Net.Http.Headers;
using System.Globalization;

namespace ControlR.Web.Server.Api;

[ApiController]
[Route(HttpConstants.AgentUpdateEndpoint)]
public class AgentUpdateController(
  ILogger<AgentUpdateController> logger,
  IWebHostEnvironment webHostEnvironment) : ControllerBase
{
  private const int CacheDurationSeconds = 600;

  private static readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
  private readonly ILogger<AgentUpdateController> _logger = logger;
  private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;

  [ResponseCache(Duration = CacheDurationSeconds, Location = ResponseCacheLocation.Any)]
  [Produces("text/plain")]
  [HttpGet("get-hash-sha256/{runtime}")]
  public async Task<ActionResult<string>> GetHash(
    RuntimeId runtime,
    [FromServices] TimeProvider timeProvider,
    CancellationToken cancellationToken)
  {
    var filePath = AppConstants.GetAgentFileDownloadPath(runtime);
    _logger.LogDebug("GetHash request started for downloads file. Path: {FilePath}", filePath);

    var fileInfo = _webHostEnvironment.WebRootFileProvider.GetFileInfo(filePath);
    if (!fileInfo.Exists || fileInfo.PhysicalPath is null)
    {
      _logger.LogWarning("File does not exist: {FilePath}", filePath);
      return NotFound();
    }

    // Ensure CDN-friendly cache headers (for Cloudflare and browsers)
    // Augment the ResponseCache filter with s-maxage and must-revalidate, and set Expires.
    Response.OnStarting(() =>
    {
      var cc = Response.Headers[HeaderNames.CacheControl].ToString();
      if (string.IsNullOrWhiteSpace(cc))
      {
        cc = $"public, max-age={CacheDurationSeconds}";
      }

      if (!cc.Contains("s-maxage", StringComparison.OrdinalIgnoreCase))
      {
        cc += $", s-maxage={CacheDurationSeconds}";
      }

      if (!cc.Contains("must-revalidate", StringComparison.OrdinalIgnoreCase))
      {
        cc += ", must-revalidate";
      }

      Response.Headers[HeaderNames.CacheControl] = cc;
      Response.Headers[HeaderNames.Expires] = timeProvider.GetUtcNow()
        .AddSeconds(CacheDurationSeconds)
        .ToString("R", CultureInfo.InvariantCulture);

      return Task.CompletedTask;
    });

    if (_memoryCache.TryGetValue(filePath, out var cachedObject) &&
        cachedObject is string cachedHash)
    {
      _logger.LogDebug("Returning hash from cache.");
      return Ok(cachedHash);
    }

    _logger.LogDebug("Calculating hash.");
    await using var fs = new FileStream(fileInfo.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    var sha256Hash = await SHA256.HashDataAsync(fs, cancellationToken);
    var hexHash = Convert.ToHexString(sha256Hash);
    _memoryCache.Set(filePath, hexHash, TimeSpan.FromSeconds(CacheDurationSeconds));

    return Ok(hexHash);
  }
}