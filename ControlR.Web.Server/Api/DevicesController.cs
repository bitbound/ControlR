using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DevicesController : ControllerBase
{
  [HttpPost]
  [AllowAnonymous]
  public async Task<ActionResult<DeviceDto>> CreateDevice(
    [FromServices] AppDb appDb,
    [FromBody] CreateDeviceRequestDto requestDto)
  {
    var deviceDto = requestDto.Device;

    if (deviceDto.Id == Guid.Empty)
    {
      return BadRequest();
    }

    if (await appDb.Devices.AnyAsync(x => x.Id == deviceDto.Id))
    {
      return BadRequest();
    }

    // TODO: Validate requestDto.InstallationKey by injecting another
    // service named IInstallationKeyManager.  This service should use
    // a MemoryCache to hold valid installation keys for lookup.
    // The keys should be generated on the Deploy.razor page when it loads.
    // The page will make an API call to an authenticated endpoint
    // that creates a new key, puts it in the cache, and returns it.

    var entity = new Device();
    var entry = appDb.Entry(entity);
    entry.State = EntityState.Added;
    entry.CurrentValues.SetValues(deviceDto);

    if (deviceDto.TagIds is { Length: > 0 } tagIds)
    {
      var tags = await appDb.Tags
        .Where(x => tagIds.Contains(x.Id))
        .ToListAsync();

      entity.Tags = tags;
    }

    await appDb.SaveChangesAsync();
    return entity.ToDto();
  }

  [HttpDelete("{deviceId:guid}")]
  public async Task<IActionResult> DeleteDevice(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromRoute] Guid deviceId)
  {
    var device = await appDb.Devices.FirstOrDefaultAsync(x => x.Id == deviceId);
    if (device is null)
    {
      return NotFound();
    }

    var authResult =
      await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    appDb.Devices.Remove(device);
    await appDb.SaveChangesAsync();
    return NoContent();
  }

  [HttpGet]
  public async IAsyncEnumerable<DeviceDto> Get(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService)
  {
    var deviceStream = appDb.Devices.AsAsyncEnumerable();

    await foreach (var device in deviceStream)
    {
      var authResult =
        await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);
      if (authResult.Succeeded)
      {
        yield return device.ToDto();
      }
    }
  }
}