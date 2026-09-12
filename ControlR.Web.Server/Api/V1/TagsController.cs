using Asp.Versioning;
using ControlR.Web.Server.Extensions.Dtos.V1;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Tags;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Tag management. Tenant scoping is enforced by the required tenantId query parameter. The
/// caller's tenant claim must match it (or the caller must be a server principal), and
/// every query carries an explicit TenantId predicate so the checks stay meaningful even for
/// server principals running against an unfiltered AppDb context. Linked device ids are
/// filtered to the devices the caller can read, preserving the device-scoped read boundary.
/// </summary>
[Route(HttpConstants.V1.TagsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class TagsController : ControllerBase
{
  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireTagsWrite)]
  [ProducesResponseType<TagResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<TagResponseDto>> Create(
    [FromServices] AppDb appDb,
    [FromQuery] Guid tenantId,
    [FromBody] TagCreateRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var tag = new Tag
    {
      TenantId = resolvedTenantId,
      Name = request.Name,
      Type = request.Type
    };

    await appDb.Tags.AddAsync(tag, cancellationToken);
    await appDb.SaveChangesAsync(cancellationToken);

    return CreatedAtAction(
      nameof(Get),
      new { tagId = tag.Id, tenantId = resolvedTenantId },
      tag.ToV1ResponseDto());
  }

  [HttpDelete("{tagId:guid}")]
  [Authorize(Policy = PolicyNames.RequireTagsWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
    [FromServices] AppDb appDb,
    [FromRoute] Guid tagId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var tag = await appDb.Tags
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == tagId && x.TenantId == resolvedTenantId, cancellationToken);

    if (tag is null)
    {
      return NotFound();
    }

    appDb.Tags.Remove(tag);
    await appDb.SaveChangesAsync(cancellationToken);

    return NoContent();
  }

  [HttpGet("{tagId:guid}")]
  [ProducesResponseType<TagResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<TagResponseDto>> Get(
    [FromServices] AppDb appDb,
    [FromServices] IDeviceAccessScopeResolver scopeResolver,
    [FromRoute] Guid tagId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var response = await BuildResponseAsync(
      appDb, scopeResolver, tagId, resolvedTenantId, cancellationToken);

    if (response is null)
    {
      return NotFound();
    }

    return Ok(response);
  }

  [HttpGet]
  [ProducesResponseType<TagsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<TagsResponseDto>> GetAll(
    [FromServices] AppDb appDb,
    [FromServices] IDeviceAccessScopeResolver scopeResolver,
    [FromQuery] Guid tenantId,
    [FromQuery] bool includeLinkedIds = false,
    CancellationToken cancellationToken = default)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var query = appDb.Tags
      .AsNoTracking()
      .Where(x => x.TenantId == resolvedTenantId);

    if (includeLinkedIds)
    {
      query = query.Include(x => x.Devices);
    }

    var tags = await query.ToListAsync(cancellationToken);

    if (includeLinkedIds)
    {
      // Only expose device IDs the caller is authorized to read, preserving the
      // device-scoped read boundary that the tag linkage would otherwise bypass.
      var readableQuery = await appDb.Devices.ApplyDeviceAccessScope(User, scopeResolver, cancellationToken);
      var readableDeviceIds = await readableQuery
        .Select(x => x.Id)
        .ToListAsync(cancellationToken);

      var readableSet = readableDeviceIds.ToHashSet();
      foreach (var tag in tags)
      {
        if (tag.Devices is { Count: > 0 })
        {
          tag.Devices = tag.Devices.Where(d => readableSet.Contains(d.Id)).ToList();
        }
      }
    }

    var items = tags
      .Select(x => x.ToV1ResponseDto())
      .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
      .ToList();

    return Ok(new TagsResponseDto { Items = items });
  }

  [HttpPut("{tagId:guid}")]
  [Authorize(Policy = PolicyNames.RequireTagsWrite)]
  [ProducesResponseType<TagResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<TagResponseDto>> Update(
    [FromServices] AppDb appDb,
    [FromServices] IDeviceAccessScopeResolver scopeResolver,
    [FromRoute] Guid tagId,
    [FromQuery] Guid tenantId,
    [FromBody] UpdateTagRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var tag = await appDb.Tags
      .FirstOrDefaultAsync(x => x.Id == tagId && x.TenantId == resolvedTenantId, cancellationToken);

    if (tag is null)
    {
      return NotFound();
    }

    tag.Name = request.Name;
    await appDb.SaveChangesAsync(cancellationToken);

    // Build the response from a detached query. Applying the read-scope filter to the tracked
    // instance would replace its Devices collection, and EF would then delete the tag's device
    // links on the next save.
    var response = await BuildResponseAsync(
      appDb, scopeResolver, tagId, resolvedTenantId, cancellationToken);

    if (response is null)
    {
      return NotFound();
    }

    return Ok(response);
  }

  private async Task<TagResponseDto?> BuildResponseAsync(
    AppDb appDb,
    IDeviceAccessScopeResolver scopeResolver,
    Guid tagId,
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    var tag = await appDb.Tags
      .AsNoTracking()
      .Include(x => x.Devices)
      .FirstOrDefaultAsync(x => x.Id == tagId && x.TenantId == tenantId, cancellationToken);

    if (tag is null)
    {
      return null;
    }

    if (tag.Devices is { Count: > 0 })
    {
      var readableQuery = await appDb.Devices.ApplyDeviceAccessScope(User, scopeResolver, cancellationToken);
      var readableSet = (await readableQuery.Select(x => x.Id).ToListAsync(cancellationToken)).ToHashSet();
      tag.Devices = tag.Devices.Where(d => readableSet.Contains(d.Id)).ToList();
    }

    return tag.ToV1ResponseDto();
  }
}
