using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api;

[Route("api/public-registration-settings")]
[ApiController]
[OutputCache(Duration = 60)]
public class PublicRegistrationSettingsController : ControllerBase
{
  [HttpGet]
  public async Task<PublicRegistrationSettings> Get(
    [FromServices] AppDb db,
    [FromServices] IOptionsMonitor<AppOptions> appOptions)
  {
    var registrationEnabled = appOptions.CurrentValue.EnablePublicRegistration || !await db.Users.AnyAsync();
    return new PublicRegistrationSettings(registrationEnabled);
  }
}
