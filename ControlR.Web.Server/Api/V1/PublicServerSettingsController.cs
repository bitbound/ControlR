using Asp.Versioning;
using ControlR.Web.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PublicServerSettings;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Server settings any caller may read, including an unauthenticated client, so the UI can
/// adapt to server configuration without a login.
/// </summary>
[Route(HttpConstants.V1.PublicServerSettingsEndpoint)]
[ApiController]
[AllowAnonymous]
[OutputCache(Duration = 60)]
[ApiVersion(ApiVersions.V1)]
public class PublicServerSettingsController(
  IPublicServerSettingsProvider serverSettings) : ControllerBase
{
  private readonly IPublicServerSettingsProvider _serverSettings = serverSettings;

  [HttpGet]
  [ProducesResponseType<PublicServerSettingsDto>(StatusCodes.Status200OK)]
  public async Task<ActionResult<PublicServerSettingsDto>> Get()
  {
    var settings = await _serverSettings.GetPublicServerSettings();
    return new PublicServerSettingsDto(
      settings.IsPublicRegistrationEnabled,
      settings.DisableDesktopPreview);
  }
}
