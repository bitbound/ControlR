using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.PublicServerSettingsEndpoint)]
[ApiController]
[OutputCache(Duration = 60)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class PublicServerSettingsController(
  IPublicServerSettingsProvider serverSettings) : ControllerBase
{
  [HttpGet]
  public async Task<InternalDtos.PublicServerSettings> Get()
  {
    var settings = await serverSettings.GetPublicServerSettings();
    return new InternalDtos.PublicServerSettings(
      settings.IsPublicRegistrationEnabled,
      settings.DisableDesktopPreview);
  }
}
