using System.Security.Cryptography;
using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api.Internal;

[ApiController]
[Route(HttpConstants.Internal.AgentUpdateEndpoint)]
[AllowAnonymous]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class InternalAgentUpdateController(
  ILogger<InternalAgentUpdateController> logger,
  IWebHostEnvironment webHostEnvironment) : ControllerBase
{
  private const int CacheDurationSeconds = 600;

  private readonly ILogger<InternalAgentUpdateController> _logger = logger;
  private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;

  [OutputCache(Duration = CacheDurationSeconds)]
  [ResponseCache(Duration = CacheDurationSeconds, Location = ResponseCacheLocation.Any)]
  [Produces("application/json")]
  [HttpGet("get-bundle-metadata/{runtime}")]
  public async Task<ActionResult<BundleMetadataDto>> GetBundleMetadata(
    RuntimeId runtime,
    [FromServices] IAgentVersionProvider agentVersionProvider,
    CancellationToken cancellationToken)
  {
    var bundlePath = GetBundleDownloadPath(runtime);
    var installerPath = GetInstallerDownloadPath(runtime);

    _logger.LogDebug("GetBundleMetadata request for runtime {Runtime}", runtime);

    var bundleFileInfo = _webHostEnvironment.WebRootFileProvider.GetFileInfo(bundlePath);
    var installerFileInfo = _webHostEnvironment.WebRootFileProvider.GetFileInfo(installerPath);

    var agentVersionResult = await agentVersionProvider.TryGetAgentVersion(cancellationToken);
    if (!agentVersionResult.IsSuccess)
    {
      _logger.LogWarning("Failed to get agent version: {Reason}", agentVersionResult.Reason);
      return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve agent version.");
    }

    if (!bundleFileInfo.Exists || bundleFileInfo.PhysicalPath is null)
    {
      _logger.LogWarning("Bundle file does not exist: {FilePath}", bundlePath);
      return NotFound("Bundle not found");
    }

    if (!installerFileInfo.Exists || installerFileInfo.PhysicalPath is null)
    {
      _logger.LogWarning("Installer file does not exist: {FilePath}", installerPath);
      return NotFound("Installer not found");
    }

    _logger.LogDebug("Computing bundle and installer hashes for {Runtime}", runtime);

    await using var bundleStream = new FileStream(bundleFileInfo.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    var bundleHash = await SHA256.HashDataAsync(bundleStream, cancellationToken);
    var bundleSha256 = Convert.ToHexString(bundleHash);

    await using var installerStream = new FileStream(installerFileInfo.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    var installerHash = await SHA256.HashDataAsync(installerStream, cancellationToken);
    var installerSha256 = Convert.ToHexString(installerHash);

    var metadata = new BundleMetadataDto
    {
      Runtime = runtime,
      Version = agentVersionResult.Value,
      BundleDownloadUrl = bundlePath,
      BundleSha256 = bundleSha256,
      InstallerDownloadUrl = installerPath,
      InstallerSha256 = installerSha256,
    };

    return Ok(metadata);
  }

  private static string GetBundleDownloadPath(RuntimeId runtime)
  {
    return AppConstants.GetBundleZipDownloadPath(runtime);
  }

  private static string GetInstallerDownloadPath(RuntimeId runtime)
  {
    return AppConstants.GetInstallerDownloadPath(runtime);
  }
}
