using Microsoft.Extensions.FileProviders;

namespace ControlR.Web.Server.Services;

public interface IAgentVersionProvider
{
  Task<Result<Version>> TryGetAgentVersion();
}

public class AgentVersionProvider(
  IWebHostEnvironment webHostEnvironment,
  ILogger<AgentVersionProvider> logger) : IAgentVersionProvider
{
  private volatile static Version? _cachedVersion;

  public async Task<Result<Version>> TryGetAgentVersion()
  {
    try
    {
      if (_cachedVersion is not null)
      {
        return Result.Ok(_cachedVersion);
      }

      var fileInfo = webHostEnvironment.WebRootFileProvider.GetFileInfo("/downloads/Version.txt");

      if (!fileInfo.Exists || string.IsNullOrWhiteSpace(fileInfo.PhysicalPath))
      {
        logger.LogError("Agent version file not found at path: {Path}", fileInfo.PhysicalPath);
        return Result.Fail<Version>("Version file not found.");
      }

      await using var fs = fileInfo.CreateReadStream();
      using var sr = new StreamReader(fs);
      var versionString = await sr.ReadToEndAsync();

      if (!Version.TryParse(versionString?.Trim(), out var version))
      {
        logger.LogError("Invalid version format in file: {VersionString}", versionString);
        return Result.Fail<Version>("Invalid version format.");
      }
      _cachedVersion = version;
      return Result.Ok(version);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error retrieving agent version.");
      return Result.Fail<Version>("Error retrieving agent version.");
    }
  }
}