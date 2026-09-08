using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.DeviceTagsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class DeviceTagsController : ControllerBase
{
  [HttpPost]
  public async Task<IActionResult> AddTag(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromBody] InternalDtos.DeviceTagAddRequestDto dto)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var device = await appDb.Devices
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == dto.DeviceId && x.TenantId == tenantId);

    if (device is null)
    {
      return NotFound("Device not found.");
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.TagsWrite);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var tag = await appDb.Tags.FirstOrDefaultAsync(x => x.Id == dto.TagId && x.TenantId == tenantId);

    if (tag is null)
    {
      return NotFound("Tag not found.");
    }

    device.Tags ??= [];
    device.Tags.Add(tag);
    await appDb.SaveChangesAsync();

    return NoContent();
  }

  [HttpDelete("{deviceId:guid}/{tagId:guid}")]
  public async Task<ActionResult<InternalDtos.TagResponseDto>> RemoveTag(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromRoute] Guid deviceId,
    [FromRoute] Guid tagId)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var device = await appDb.Devices
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == deviceId && x.TenantId == tenantId);

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
    await appDb.SaveChangesAsync();

    return NoContent();
  }
}