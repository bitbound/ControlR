using System.Collections.Immutable;
using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("api/user-tags")]
[ApiController]
[Authorize]
public class UserTagsController : ControllerBase
{
  [HttpPost]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<IActionResult> AddTag(
    [FromServices] AppDb appDb,
    [FromBody] UserTagAddRequestDto dto)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var user = await appDb.Users
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == dto.UserId);

    if (user is null)
    {
      return NotFound("User not found.");
    }

    if (user.TenantId != tenantId)
    {
      return Unauthorized();
    }

    var tag = await appDb.Tags.FirstOrDefaultAsync(x => x.Id == dto.TagId);

    if (tag is null)
    {
      return NotFound("Tag not found.");
    }
    if (tag.TenantId != tenantId)
    {
      return Unauthorized();
    }

    user.Tags ??= [];
    user.Tags.Add(tag);
    await appDb.SaveChangesAsync();
    return NoContent();
  }


  [HttpGet]
  public async Task<ActionResult<TagResponseDto[]>> GetAllowedTags(
    [FromServices] AppDb appDb)
  {
    if (User.IsInRole(RoleNames.TenantAdministrator))
    {
      var tags = await appDb.Tags
        .AsNoTracking()
        .ToListAsync();

      return Ok(tags
        .Select(x => x.ToDto())
        .ToArray());
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var user = await appDb.Users
      .AsNoTracking()
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == userId);

    if (user is null)
    {
      return Unauthorized();
    }

    if (user.Tags is not { Count: > 0 })
    {
      return Ok(Array.Empty<TagResponseDto>());
    }

    var userTags = user.Tags
      .Select(x => x.ToDto())
      .ToArray();

    return Ok(userTags);
  }

  [HttpGet("{userId:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<TagResponseDto[]>> GetUserTags(
    [FromRoute] Guid userId,
    [FromServices] AppDb appDb)
  {
    var user = await appDb.Users
      .AsNoTracking()
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == userId);

    if (user is null)
    {
      return Unauthorized();
    }

    if (user.Tags is not { Count: > 0 })
    {
      return Ok(Array.Empty<TagResponseDto>());
    }

    var userTags = user.Tags
      .Select(x => x.ToDto())
      .ToArray();

    return Ok(userTags);
  }

  [HttpDelete("{userId:guid}/{tagId:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<TagResponseDto>> RemoveTag(
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
      .FirstOrDefaultAsync(x => x.Id == userId);

    if (user is null)
    {
      return NotFound("User not found.");
    }

    if (user.TenantId != tenantId)
    {
      return Unauthorized();
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