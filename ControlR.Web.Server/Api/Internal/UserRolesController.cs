using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.UserRolesEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class UserRolesController(ILogger<UserRolesController> logger) : ControllerBase
{
  private readonly ILogger<UserRolesController> _logger = logger;

  [HttpPost]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<IActionResult> AddRole(
    [FromServices] AppDb appDb,
    [FromBody] InternalDtos.UserRoleAddRequestDto dto)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var user = await appDb.Users
      .Include(x => x.UserRoles)
      .FirstOrDefaultAsync(x => x.Id == dto.UserId && x.TenantId == tenantId);

    if (user is null)
    {
      return NotFound("User not found.");
    }

    var role = await appDb.Roles.FirstOrDefaultAsync(x => x.Id == dto.RoleId);

    if (role is null)
    {
      return NotFound("Role not found.");
    }

    if (role.Name == RoleNames.ServerAdministrator &&
        !User.IsInRole(RoleNames.ServerAdministrator))
    {
      _logger.LogCritical(
        "Non-server-admin user ({UserName}) attempted to add user {UserId} as a server admin.",
        user.UserName,
        dto.UserId);

      return Forbid();
    }

    var userRole = new IdentityUserRole<Guid>()
    {
      RoleId = role.Id,
      UserId = user.Id
    };
    user.UserRoles ??= [];
    user.UserRoles.Add(userRole);
    await appDb.SaveChangesAsync();
    return NoContent();
  }

  [HttpGet]
  public async Task<ActionResult<InternalDtos.RoleResponseDto[]>> GetOwnRoles(
    [FromServices] AppDb appDb)
  {

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var user = await appDb.Users
      .AsNoTracking()
      .Include(u => u.UserRoles)
      .Select(u => new
      {
        User = u,
        Roles = appDb.Roles
          .Include(r => r.UserRoles)
          .Where(r => u.UserRoles!.Any(ur => ur.RoleId == r.Id)).ToList()
      })
      .FirstOrDefaultAsync(x => x.User.Id == userId);

    if (user is null)
    {
      return Unauthorized();
    }

    if (user.Roles is not { Count: > 0 })
    {
      return Ok(Array.Empty<InternalDtos.RoleResponseDto>());
    }

    var userRoles = user.Roles
      .Select(x => x.ToInternalResponseDto())
      .ToArray();

    return Ok(userRoles);
  }

  [HttpGet("{userId:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<InternalDtos.RoleResponseDto[]>> GetUserRoles(
    [FromRoute] Guid userId,
    [FromServices] AppDb appDb)
  {
    var user = await appDb.Users
      .AsNoTracking()
      .Include(u => u.UserRoles)
      .Select(u => new
      {
        User = u,
        Roles = appDb.Roles
          .Include(r => r.UserRoles)
          .Where(r => u.UserRoles!.Any(ur => ur.RoleId == r.Id)).ToList()
      })
      .FirstOrDefaultAsync(x => x.User.Id == userId);

    if (user is null)
    {
      return Unauthorized();
    }

    if (user.Roles is not { Count: > 0 })
    {
      return Ok(Array.Empty<InternalDtos.RoleResponseDto>());
    }

    var userRoles = user.Roles
      .Select(x => x.ToInternalResponseDto())
      .ToArray();

    return Ok(userRoles);
  }

  [HttpDelete("{userId:guid}/{roleId:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<InternalDtos.RoleResponseDto>> RemoveRole(
    [FromServices] AppDb appDb,
    [FromRoute] Guid userId,
    [FromRoute] Guid roleId)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    if (!User.TryGetUserId(out var callerUserId))
    {
      return Unauthorized();
    }

    var user = await appDb.Users
      .Include(x => x.UserRoles)
      .FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId);

    if (user is null)
    {
      return NotFound("User not found.");
    }

    var role = await appDb.Roles.FindAsync(roleId);
    if (role is null)
    {
      return NotFound();
    }

    if (role.Name == RoleNames.ServerAdministrator &&
        !User.IsInRole(RoleNames.ServerAdministrator))
    {
      _logger.LogCritical(
         "Non-server-admin user ({UserName}) attempted to remove user {UserId} as a server admin.",
         user.UserName,
         userId);

      return Forbid();
    }

    if (user.Id == callerUserId &&
       (role.Name == RoleNames.ServerAdministrator || role.Name == RoleNames.TenantAdministrator))
    {
      _logger.LogWarning(
        "User {UserName} attempted to remove self from role {RoleName}.",
        user.UserName,
        role.Name);
      return BadRequest("You cannot remove yourself from this role.");
    }

    user.UserRoles ??= [];
    var userRole = user.UserRoles.Find(x => x.RoleId == roleId);
    if (userRole is null)
    {
      return NotFound("Tag not found on user.");
    }
    user.UserRoles.Remove(userRole);
    await appDb.SaveChangesAsync();
    return NoContent();
  }
}
