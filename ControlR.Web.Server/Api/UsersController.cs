using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = RoleNames.TenantAdministrator)]
public class UsersController : ControllerBase
{
  [HttpGet]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<List<UserResponseDto>>> GetAll(
    [FromServices] AppDb appDb)
  {
    return await appDb.Users
      .Select(x => new UserResponseDto(x.Id, x.UserName, x.Email))
      .ToListAsync();
  }

  [HttpDelete("{id:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid id,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] AppDb appDb)
  {
    var user = await appDb.Users
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.Id == id);

    if (user == null)
    {
      return NotFound();
    }

    var result = await userManager.DeleteAsync(user);
    if (!result.Succeeded)
    {
      return BadRequest(result.Errors.Select(e => e.Description));
    }

    return NoContent();
  }
}