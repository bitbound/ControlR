using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DevicesController : ControllerBase
{
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
  public async IAsyncEnumerable<DeviceUpdateResponseDto> Get(
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