using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[OutputCache(Duration = 60)]
public class VersionController(IAgentVersionProvider agentVersionProvider) : ControllerBase
{
  [HttpGet("agent")]
  [OutputCache]
  public async Task<ActionResult<Version>> GetCurrentAgentVersion()
  {
    var result = await agentVersionProvider.TryGetAgentVersion();
    if (!result.IsSuccess)
    {
      return NotFound();
    }
    return Ok(result.Value);
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