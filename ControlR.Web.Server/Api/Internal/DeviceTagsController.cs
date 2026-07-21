using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.DeviceTagsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class DeviceTagsController : ControllerBase
{
  [HttpPost]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<IActionResult> AddTag(
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub> agentHub,
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

    var tag = await appDb.Tags.FirstOrDefaultAsync(x => x.Id == dto.TagId && x.TenantId == tenantId);

    if (tag is null)
    {
      return NotFound("Tag not found.");
    }

    device.Tags ??= [];
    device.Tags.Add(tag);
    await appDb.SaveChangesAsync();

    await agentHub.Groups.RemoveFromGroupAsync(
        device.ConnectionId,
        HubGroupNames.GetTagGroupName(dto.TagId, tag.TenantId));

    return NoContent();
  }

  [HttpDelete("{deviceId:guid}/{tagId:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<InternalDtos.TagResponseDto>> RemoveTag(
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub> agentHub,
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

    device.Tags ??= [];
    var tag = device.Tags.Find(x => x.Id == tagId);
    if (tag is null)
    {
      return NotFound("Tag not found on device.");
    }
    device.Tags.Remove(tag);
    await appDb.SaveChangesAsync();

    await agentHub.Groups.RemoveFromGroupAsync(
      device.ConnectionId,
      HubGroupNames.GetTagGroupName(tagId, tag.TenantId));

    return NoContent();
  }
}