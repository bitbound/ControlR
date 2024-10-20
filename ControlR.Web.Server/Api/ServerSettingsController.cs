using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

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
    [FromServices] IOptionsMonitor<AppOptions> appOptions)
  {
    var registrationEnabled = !await db.Users.AnyAsync() || appOptions.CurrentValue.EnablePublicRegistration;
    return new ServerSettingsDto(registrationEnabled);
  }
}
