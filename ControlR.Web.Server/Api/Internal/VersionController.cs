using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.VersionEndpoint)]
[ApiController]
[OutputCache(Duration = 60)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class VersionController(
  IAgentVersionProvider agentVersionProvider,
  IReleaseNotesProvider releaseNotesProvider) : ControllerBase
{
  [HttpGet("agent")]
  [OutputCache]
  public async Task<ActionResult<Version>> GetCurrentAgentVersion(CancellationToken cancellationToken)
  {
    var result = await agentVersionProvider.TryGetAgentVersion(cancellationToken);
    return result.ToActionResult();
  }

  [HttpGet("release-notes")]
  [OutputCache]
  public async Task<ActionResult<string>> GetReleaseNotes(CancellationToken cancellationToken)
  {
    var result = await releaseNotesProvider.GetReleaseNotes(cancellationToken);
    return result.ToActionResult();
  }

  [HttpGet("server")]
  [OutputCache]
  public ActionResult<Version> GetServerVersion()
  {
    var version = typeof(VersionController)
      .Assembly
      .GetName()
      ?.Version;

    if (version is null)
    {
      return NotFound();
    }

    return Ok(version);
  }
}