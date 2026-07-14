using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Constants;
 
namespace ControlR.Web.Server.Api.Internal;
 
[Route(HttpConstants.Internal.RolesEndpoint)]
[Route(HttpConstants.Legacy.RolesEndpoint)]
[ApiController]
[Authorize(Roles = RoleNames.TenantAdministrator)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class RolesController : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<InternalDtos.RoleResponseDto[]>> GetAll(
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

    var dtos = roles.Select(x => new InternalDtos.RoleResponseDto(x.Id, x.Name, [.. x.UserIds]));
    return Ok(dtos);
  }
}
