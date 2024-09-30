using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[OutputCache(Duration = 60)]
[Authorize]
public class DevicesController : ControllerBase
{

  [HttpGet]
  public async Task<ActionResult<List<DeviceDto>>> Get(
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService)
  {
    var user = await userManager.GetUserAsync(User) ??
      throw new InvalidOperationException("Unable to find user.");

    var deviceQuery = appDb.Devices.Where(x => x.TenantId == user.TenantId);
    var authorizedDevices = new List<DeviceDto>();

    await foreach (var device in deviceQuery.AsAsyncEnumerable())
    {
      var authResult = await authorizationService.AuthorizeAsync(User, device, RemoteControlByDevicePolicy.PolicyName);
      if (authResult.Succeeded)
      {
        authorizedDevices.Add(device.ToDto());
      }
    }

    return authorizedDevices;
  }
}
