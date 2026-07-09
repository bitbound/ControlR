using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.TagsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class TagsController : ControllerBase
{
  [HttpPost]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<TagResponseDto>> CreateTag(
    [FromServices] AppDb appDb,
    [FromBody] TagCreateRequestDto dto)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return NotFound("User tenant not found.");
      tenantId = tid;
    }

    if (!tenantId.HasValue)
    {
      return BadRequest("Server service accounts cannot create tags.");
    }

    var tag = new Tag
    {
      TenantId = tenantId.Value,
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
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return NotFound("User tenant not found.");
      tenantId = tid;
    }

    var tag = await appDb.Tags
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == tagId && (!tenantId.HasValue || x.TenantId == tenantId.Value));

    if (tag == null)
    {
      return NotFound();
    }

    appDb.Tags.Remove(tag);
    await appDb.SaveChangesAsync();

    return NoContent();
  }

  [HttpGet]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<TagResponseDto[]>> GetAllTags(
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
      .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return Ok(dtos);
  }

  [HttpPut]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<TagResponseDto>> RenameTag(
    [FromServices] AppDb appDb,
    [FromBody] TagRenameRequestDto dto)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return NotFound("User tenant not found.");
      tenantId = tid;
    }

    var tag = await appDb.Tags
      .FirstOrDefaultAsync(x => x.Id == dto.TagId && (!tenantId.HasValue || x.TenantId == tenantId.Value));
    if (tag is null)
    {
      return NotFound();
    }

    tag.Name = dto.NewTagName;
    await appDb.SaveChangesAsync();
    return tag.ToDto();
  }
}