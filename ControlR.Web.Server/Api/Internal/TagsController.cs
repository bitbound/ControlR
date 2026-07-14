using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.TagsEndpoint)]
[Route(HttpConstants.Legacy.TagsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class TagsController : ControllerBase
{
  [HttpPost]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<InternalDtos.TagResponseDto>> CreateTag(
    [FromServices] AppDb appDb,
    [FromBody] InternalDtos.TagCreateRequestDto dto)
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

    return Ok(tag.ToInternalResponseDto());
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
      .FirstOrDefaultAsync(x => x.Id == tagId && x.TenantId == tenantId);

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
  public async Task<ActionResult<InternalDtos.TagResponseDto[]>> GetAllTags(
    [FromServices] AppDb appDb,
    [FromQuery] bool includeLinkedIds = false)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var query = appDb.Tags
      .AsNoTracking()
      .Where(x => x.TenantId == tenantId);

    if (includeLinkedIds)
    {
      query = query
        .Include(x => x.Users)
        .Include(x => x.Devices);
    }

    // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataQuery
    var tags = await query.ToListAsync();

    var dtos = tags
      .Select(x => x.ToInternalResponseDto())
      .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return Ok(dtos);
  }

  [HttpPut]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<InternalDtos.TagResponseDto>> RenameTag(
    [FromServices] AppDb appDb,
    [FromBody] InternalDtos.TagRenameRequestDto dto)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var tag = await appDb.Tags
      .FirstOrDefaultAsync(x => x.Id == dto.TagId && x.TenantId == tenantId);
    if (tag is null)
    {
      return NotFound();
    }

    tag.Name = dto.NewTagName;
    await appDb.SaveChangesAsync();
    return tag.ToInternalResponseDto();
  }
}
