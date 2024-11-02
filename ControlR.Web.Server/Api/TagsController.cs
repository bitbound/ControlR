using System.Collections.Immutable;
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
  public async Task<ActionResult<List<TagDto>>> GetAllTags(
    [FromServices] AppDb appDb,
    [FromQuery] bool includeLinkedIds = false)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var query = appDb.Tags.AsQueryable();

    if (includeLinkedIds)
    {
      query = query
        .Include(x => x.Users)
        .Include(x => x.Devices);
    }

    var tags = await query
      .AsNoTracking()
      .Where(x => x.TenantId == tenantId)
      .Select(x => new
      {
        x.Id,
        x.Name,
        x.Type,
        Users = x.Users!.Select(u => u.Id),
        Devices = x.Devices!.Select(d => d.Id)
      })
      .ToListAsync();

    var dtos = tags
      .Select(x => new TagDto(
        x.Id,
        x.Name,
        x.Type,
        x.Users.ToImmutableArray(),
        x.Devices.ToImmutableArray()))
      .ToList();

    return Ok(dtos);
  }
}