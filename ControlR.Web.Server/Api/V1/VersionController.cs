using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Public version discovery: the current agent version, the server version, and the release
/// notes. Anonymous by design, like the internal surface it supersedes, so a client can learn
/// the versions before authenticating.
/// </summary>
[Route(HttpConstants.V1.VersionEndpoint)]
[ApiController]
[AllowAnonymous]
[OutputCache(Duration = 60)]
[ApiVersion(ApiVersions.V1)]
public class VersionController(
  IAgentVersionProvider agentVersionProvider,
  IReleaseNotesProvider releaseNotesProvider) : ControllerBase
{
  private readonly IAgentVersionProvider _agentVersionProvider = agentVersionProvider;
  private readonly IReleaseNotesProvider _releaseNotesProvider = releaseNotesProvider;

  [HttpGet("agent")]
  [OutputCache]
  public async Task<ActionResult<Version>> GetCurrentAgentVersion(CancellationToken cancellationToken)
  {
    var result = await _agentVersionProvider.TryGetAgentVersion(cancellationToken);
    return result.ToActionResult();
  }

  [HttpGet("release-notes")]
  [OutputCache]
  public async Task<ActionResult<string>> GetReleaseNotes(CancellationToken cancellationToken)
  {
    var result = await _releaseNotesProvider.GetReleaseNotes(cancellationToken);
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
