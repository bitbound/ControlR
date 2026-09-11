using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using DeviceTagsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceTags;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Adding and removing tags on a device. The tenantId query parameter pins the tenant for
/// resource loading (explicit predicates, meaningful under unfiltered server contexts), and
/// the mutation itself is gated by the device-scoped tags.write resource policy.
/// </summary>
[Route(HttpConstants.V1.DeviceTagsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class DeviceTagsController : ControllerBase
{
  [HttpPost]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Add(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromQuery] Guid tenantId,
    [FromBody] DeviceTagsDtos.DeviceTagAddRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var device = await appDb.Devices
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == request.DeviceId && x.TenantId == resolvedTenantId, cancellationToken);

    if (device is null)
    {
      return NotFound("Device not found.");
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.TagsWrite);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var tag = await appDb.Tags
      .FirstOrDefaultAsync(x => x.Id == request.TagId && x.TenantId == resolvedTenantId, cancellationToken);

    if (tag is null)
    {
      return NotFound("Tag not found.");
    }

    device.Tags ??= [];
    device.Tags.Add(tag);
    await appDb.SaveChangesAsync(cancellationToken);

    return NoContent();
  }

  [HttpDelete("{deviceId:guid}/{tagId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Remove(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromRoute] Guid deviceId,
    [FromRoute] Guid tagId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var device = await appDb.Devices
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == deviceId && x.TenantId == resolvedTenantId, cancellationToken);

    if (device is null)
    {
      return NotFound("Device not found.");
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.TagsWrite);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    device.Tags ??= [];
    var tag = device.Tags.Find(x => x.Id == tagId);
    if (tag is null)
    {
      return NotFound("Tag not found on device.");
    }

    device.Tags.Remove(tag);
    await appDb.SaveChangesAsync(cancellationToken);

    return NoContent();
  }
}
