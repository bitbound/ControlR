using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Services.Repositories;
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
    [FromServices] IRepository repo)
  {
    if (!User.TryGetTenantUid(out var tenantUid))
    {
      return NotFound("User tenant not found.");
    }

    // TODO
    return Ok(new List<DeviceGroupDto>());
  }
}
