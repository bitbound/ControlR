using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.PublicRegistrationSettingsEndpoint)]
[ApiController]
[OutputCache(Duration = 60)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class PublicRegistrationSettingsController : ControllerBase
{
  [HttpGet]
  public async Task<PublicRegistrationSettings> Get(
    [FromServices] AppDb db,
    [FromServices] IOptionsMonitor<AppOptions> appOptions)
  {
    var hasUsers = await db.Users.AnyAsync();
    var registrationEnabled = appOptions.CurrentValue.EnablePublicRegistration ||
      (appOptions.CurrentValue.EnableFirstUserBootstrap && !hasUsers);
    return new PublicRegistrationSettings(registrationEnabled);
  }
}
