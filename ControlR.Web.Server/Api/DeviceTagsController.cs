using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Api;

[Route("api/device-tags")]
[ApiController]
[Authorize]
public class DeviceTagsController : ControllerBase
{
  [HttpPost]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<IActionResult> AddTag(
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub> agentHub,
    [FromBody] DeviceTagAddRequestDto dto)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var device = await appDb.Devices
      .Include(x => x.Tags)
      .FirstOrDefaultAsync(x => x.Id == dto.DeviceId);

    if (device is null)
    {
      return NotFound("Device not found.");
    }

    if (device.TenantId != tenantId)
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

    device.Tags ??= [];
    device.Tags.Add(tag);
    await appDb.SaveChangesAsync();

    await agentHub.Groups.RemoveFromGroupAsync(
        device.ConnectionId,
        HubGroupNames.GetTagGroupName(dto.TagId, tenantId));

    return NoContent();
  }

  [HttpDelete("{deviceId:guid}/{tagId:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<TagResponseDto>> RemoveTag(
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
      .FirstOrDefaultAsync(x => x.Id == deviceId);

    if (device is null)
    {
      return NotFound("User not found.");
    }

    if (device.TenantId != tenantId)
    {
      return Unauthorized();
    }

    device.Tags ??= [];
    var tag = device.Tags.Find(x => x.Id == tagId);
    if (tag is null)
    {
      return NotFound("Tag not found on user.");
    }
    device.Tags.Remove(tag);
    await appDb.SaveChangesAsync();

    await agentHub.Groups.RemoveFromGroupAsync(
      device.ConnectionId,
      HubGroupNames.GetTagGroupName(tagId, tenantId));

    return NoContent();
  }
}