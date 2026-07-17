using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services;

public interface IAgentVersionProvider
{
  Task<bool> IsAgentOutdated(string? deviceAgentVersion, CancellationToken cancellationToken = default);
  Task<HttpResult<Version>> TryGetAgentVersion(CancellationToken cancellationToken = default);
}

public class AgentVersionProvider(
  IWebHostEnvironment webHostEnvironment,
  ILogger<AgentVersionProvider> logger) : IAgentVersionProvider
{
  private static readonly SemaphoreSlim _versionLock = new(1, 1);
  private static volatile Version? _cachedVersion;

  public async Task<bool> IsAgentOutdated(string? deviceAgentVersion, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(deviceAgentVersion))
    {
      return false;
    }

    var agentVersionResult = await TryGetAgentVersion(cancellationToken);
    return agentVersionResult.IsSuccess && deviceAgentVersion != agentVersionResult.Value.ToString();
  }

  public async Task<HttpResult<Version>> TryGetAgentVersion(CancellationToken cancellationToken = default)
  {
    try
    {
      if (webHostEnvironment.IsDevelopment())
      {
        _cachedVersion = typeof(AgentVersionProvider).Assembly.GetName()?.Version;
      }

      if (_cachedVersion is not null)
      {
        return HttpResult.Ok(_cachedVersion);
      }

      using var heldLock = await _versionLock.AcquireLockAsync(cancellationToken);

      if (_cachedVersion is not null)
      {
        return HttpResult.Ok(_cachedVersion);
      }

      var fileInfo = webHostEnvironment.WebRootFileProvider.GetFileInfo("/downloads/Version.txt");

      if (!fileInfo.Exists || string.IsNullOrWhiteSpace(fileInfo.PhysicalPath))
      {
        logger.LogError("Agent version file not found at path: {Path}", fileInfo.PhysicalPath);
        return HttpResult.Fail<Version>(HttpResultErrorCode.NotFound, "Version file not found.");
      }

      await using var fs = new FileStream(fileInfo.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
      using var sr = new StreamReader(fs);
      var versionString = await sr.ReadToEndAsync(cancellationToken);

      if (!Version.TryParse(versionString?.Trim(), out var version))
      {
        logger.LogError("Invalid version format in file: {VersionString}", versionString);
        return HttpResult.Fail<Version>(HttpResultErrorCode.InternalServerError, "Invalid version format.");
      }
      _cachedVersion = version;
      return HttpResult.Ok(version);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error retrieving agent version.");
      return HttpResult.Fail<Version>(ex, HttpResultErrorCode.InternalServerError, "Error retrieving agent version.");
    }
  }
}