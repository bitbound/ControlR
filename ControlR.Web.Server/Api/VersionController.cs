using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.FileProviders;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[OutputCache(Duration = 60)]
public class VersionController(IFileProvider phyiscalFileProvider) : ControllerBase
{
  [HttpGet("agent")]
  [OutputCache]
  public async Task<ActionResult<Version>> GetCurrentAgentVersion()
  {
    var fileInfo = phyiscalFileProvider.GetFileInfo("/wwwroot/downloads/AgentVersion.txt");

    if (!fileInfo.Exists || string.IsNullOrWhiteSpace(fileInfo.PhysicalPath))
    {
      return NotFound();
    }

    await using var fs = fileInfo.CreateReadStream();
    using var sr = new StreamReader(fs);
    var versionString = await sr.ReadToEndAsync();

    if (!Version.TryParse(versionString?.Trim(), out var version))
    {
      return NotFound();
    }

    return Ok(version);
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