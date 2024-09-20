using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.FileProviders;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
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

  [HttpGet("viewer")]
  [OutputCache]
  public async Task<ActionResult<Version>> GetCurrentViewerVersion()
  {
    var fileInfo = phyiscalFileProvider.GetFileInfo("/wwwroot/downloads/ViewerVersion.txt");

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
}