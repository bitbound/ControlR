using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.PublicRegistrationSettingsEndpoint)]
[Route(HttpConstants.Legacy.PublicRegistrationSettingsEndpoint)]
[ApiController]
[OutputCache(Duration = 60)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class PublicRegistrationSettingsController : ControllerBase
{
  [HttpGet]
  public async Task<InternalDtos.PublicRegistrationSettings> Get(
    [FromServices] AppDb db,
    [FromServices] IOptionsMonitor<AppOptions> appOptions)
  {
    var hasUsers = await db.Users.AnyAsync();
    var registrationEnabled = appOptions.CurrentValue.EnablePublicRegistration ||
      (!appOptions.CurrentValue.DisableFirstUserSelfRegistration && !hasUsers);
    return new InternalDtos.PublicRegistrationSettings(registrationEnabled);
  }
}
