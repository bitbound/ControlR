using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.UserTagsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class UserTagsController : ControllerBase
{
  [HttpPost]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<IActionResult> AddTag(
    [FromServices] AppDb appDb,
    [FromBody] InternalDtos.UserTagAddRequestDto dto)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var user = await appDb.Users
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == dto.UserId && x.TenantId == tenantId);

    if (user is null)
    {
      return NotFound("User not found.");
    }

    var tag = await appDb.Tags.FirstOrDefaultAsync(x => x.Id == dto.TagId && x.TenantId == tenantId);

    if (tag is null)
    {
      return NotFound("Tag not found.");
    }

    user.Tags ??= [];
    user.Tags.Add(tag);
    await appDb.SaveChangesAsync();
    return NoContent();
  }

  [HttpGet]
  public async Task<ActionResult<InternalDtos.TagResponseDto[]>> GetAllowedTags(
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    if (User.IsInRole(RoleNames.TenantAdministrator))
    {
      var tags = await appDb.Tags
        .AsNoTracking()
        .Where(x => x.TenantId == tenantId)
        .ToListAsync();

      return Ok(tags
        .Select(x => x.ToInternalResponseDto())
        .ToArray());
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var user = await appDb.Users
      .AsNoTracking()
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId);

    if (user is null)
    {
      return Unauthorized();
    }

    if (user.Tags is not { Count: > 0 })
    {
      return Ok(Array.Empty<InternalDtos.TagResponseDto>());
    }

    var userTags = user.Tags
      .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
      .Select(x => x.ToInternalResponseDto())
      .ToArray();

    return Ok(userTags);
  }

  [HttpGet("{userId:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<InternalDtos.TagResponseDto[]>> GetUserTags(
    [FromRoute] Guid userId,
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var user = await appDb.Users
      .AsNoTracking()
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId);

    if (user is null)
    {
      return Unauthorized();
    }

    if (user.Tags is not { Count: > 0 })
    {
      return Ok(Array.Empty<InternalDtos.TagResponseDto>());
    }

    var userTags = user.Tags
      .Select(x => x.ToInternalResponseDto())
      .ToArray();

    return Ok(userTags);
  }

  [HttpDelete("{userId:guid}/{tagId:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<InternalDtos.TagResponseDto>> RemoveTag(
    [FromServices] AppDb appDb,
    [FromRoute] Guid userId,
    [FromRoute] Guid tagId)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var user = await appDb.Users
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId);

    if (user is null)
    {
      return NotFound("User not found.");
    }

    user.Tags ??= [];
    var tag = user.Tags.Find(x => x.Id == tagId);
    if (tag is null)
    {
      return NotFound("Tag not found on user.");
    }
    user.Tags.Remove(tag);
    await appDb.SaveChangesAsync();
    return NoContent();
  }
}
