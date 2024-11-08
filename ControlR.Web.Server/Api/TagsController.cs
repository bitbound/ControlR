using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TagsController : ControllerBase
{
  [HttpGet]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<List<TagResponseDto>>> GetAllTags(
    [FromServices] AppDb appDb,
    [FromQuery] bool includeLinkedIds = false)
  {
    var query = appDb.Tags.AsNoTracking();

    if (includeLinkedIds)
    {
      query = query
        .Include(x => x.Users)
        .Include(x => x.Devices);
    }

    // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataQuery
    var tags = await query.ToListAsync();

    var dtos = tags
      .Select(x => x.ToDto())
      .ToList();

    return Ok(dtos);
  }
  
  [HttpPost]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<TagResponseDto>> CreateTag(
    [FromServices] AppDb appDb,
    [FromBody] TagCreateRequestDto dto)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var tag = new Tag
    {
      TenantId = tenantId,
      Name = dto.Name,
      Type = dto.Type,
    };

    await appDb.Tags.AddAsync(tag);
    await appDb.SaveChangesAsync();
    
    return Ok(tag.ToDto());
  }
  
  [HttpDelete("{tagId:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult> DeleteTag(
    [FromServices] AppDb appDb,
    [FromRoute] Guid tagId)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var tag = await appDb.Tags
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == tagId);

    if (tag == null)
    {
      return NotFound();
    }

    appDb.Tags.Remove(tag);
    await appDb.SaveChangesAsync();
    
    return NoContent();
  }
}