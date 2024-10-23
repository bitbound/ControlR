﻿using Microsoft.AspNetCore.Authorization;
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
  public async IAsyncEnumerable<DeviceResponseDto> Get(
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService)
  {
    var user = await userManager.GetUserAsync(User) ??
      throw new InvalidOperationException("Unable to find user.");

    var deviceQuery = appDb.Devices
      .AsNoTracking()
      .Where(x => x.TenantId == user.TenantId);

    await foreach (var device in deviceQuery.AsAsyncEnumerable())
    {
      var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);
      if (authResult.Succeeded)
      {
        yield return device.ToDto();
      }
    }
  }
}
