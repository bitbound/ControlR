using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[OutputCache(Duration = 60)]
[Authorize]
public class ServerSettingsController : ControllerBase
{
  [HttpGet]
  public async Task<ServerSettingsDto> Get(
    [FromServices] AppDb db,
    [FromServices] IOptionsMonitor<ApplicationOptions> applicationOptions)
  {
    var userCount = await db.Users.CountAsync();
    var registrationEnabled = userCount == 0 || applicationOptions.CurrentValue.EnablePublicRegistration;
    return new ServerSettingsDto(registrationEnabled);
  }
}
