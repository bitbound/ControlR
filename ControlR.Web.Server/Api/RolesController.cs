using Microsoft.AspNetCore.Mvc;
using System.Collections.Immutable;

namespace ControlR.Web.Server.Api;

[Route("api/roles")]
[ApiController]
[Authorize(Roles = RoleNames.TenantAdministrator)]
public class RolesController : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<RoleResponseDto[]>> GetAll(
    [FromServices] RoleManager<AppRole> roleManager)
  {
    var roles = await roleManager.Roles
      .AsNoTracking()
      .Include(r => r.UserRoles)
      .Select(r => new
      {
        r.Id,
        Name = r.Name ?? "",
        UserIds = r.UserRoles!.Select(u => u.UserId)
      })
      .ToListAsync();

    var dtos = roles.Select(x => new RoleResponseDto(x.Id, x.Name, [.. x.UserIds]));
    return Ok(dtos);
  }
}
