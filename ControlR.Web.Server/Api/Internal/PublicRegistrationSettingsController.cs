using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.PublicRegistrationSettingsEndpoint)]
[Route(HttpConstants.Legacy.PublicRegistrationSettingsEndpoint)]
[ApiController]
[OutputCache(Duration = 60)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class PublicRegistrationSettingsController(
  IPublicRegistrationSettingsProvider registrationSettings) : ControllerBase
{
  [HttpGet]
  public async Task<InternalDtos.PublicRegistrationSettings> Get()
  {
    var isEnabled = await registrationSettings.GetIsPublicRegistrationEnabled();
    return new InternalDtos.PublicRegistrationSettings(isEnabled);
  }
}
