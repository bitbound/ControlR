using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api;

[Route("api/server-settings")]
[ApiController]
[OutputCache(Duration = 60)]
public class ServerSettingsController : ControllerBase
{
  [HttpGet]
  public async Task<ServerSettingsDto> Get(
    [FromServices] AppDb db,
    [FromServices] IOptionsMonitor<AppOptions> appOptions)
  {
    var registrationEnabled = appOptions.CurrentValue.EnablePublicRegistration || !await db.Users.AnyAsync();
    return new ServerSettingsDto(registrationEnabled);
  }
}
