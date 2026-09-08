using ControlR.Web.Server.Extensions.Dtos.Internal;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.TagsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class TagsController : ControllerBase
{
  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireTagsWrite)]
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
  [Authorize(Policy = PolicyNames.RequireTagsWrite)]
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
  public async Task<ActionResult<InternalDtos.TagResponseDto[]>> GetAllTags(
    [FromServices] AppDb appDb,
    [FromServices] IDeviceAccessScopeResolver scopeResolver,
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
        .Include(x => x.Devices);
    }

    // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataQuery
    var tags = await query.ToListAsync();

    if (includeLinkedIds)
    {
      // Only expose device IDs the caller is authorized to read, preserving the
      // device-scoped read boundary that the tag linkage would otherwise bypass.
      var readableQuery = await appDb.Devices.ApplyDeviceAccessScope(User, scopeResolver);
      var readableDeviceIds = await readableQuery
        .Select(x => x.Id)
        .ToListAsync();

      var readableSet = readableDeviceIds.ToHashSet();
      foreach (var tag in tags)
      {
        if (tag.Devices is { Count: > 0 })
        {
          tag.Devices = tag.Devices.Where(d => readableSet.Contains(d.Id)).ToList();
        }
      }
    }

    var dtos = tags
      .Select(x => x.ToInternalResponseDto())
      .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return Ok(dtos);
  }

  [HttpPut]
  [Authorize(Policy = PolicyNames.RequireTagsWrite)]
  public async Task<ActionResult<InternalDtos.TagResponseDto>> RenameTag(
    [FromServices] AppDb appDb,
    [FromBody] InternalDtos.TagRenameRequestDto dto)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var tag = await appDb.Tags
      .Include(x => x.Devices)
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
