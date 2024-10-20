using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api;

[Route("api/device-groups")]
[ApiController]
[OutputCache(Duration = 60)]
[Authorize]
public class DeviceGroupsController : ControllerBase
{

  [HttpGet]
  public async Task<ActionResult<List<DeviceGroupDto>>> Get(
    [FromServices] AppDb db)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    // TODO
    await Task.Yield();
    return Ok(new List<DeviceGroupDto>());
  }
}
