using Asp.Versioning;
using ControlR.Web.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using PublicSettingsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PublicServerSettings;

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
  [ProducesResponseType<PublicSettingsDtos.PublicServerSettingsDto>(StatusCodes.Status200OK)]
  public async Task<ActionResult<PublicSettingsDtos.PublicServerSettingsDto>> Get()
  {
    var settings = await _serverSettings.GetPublicServerSettings();
    return new PublicSettingsDtos.PublicServerSettingsDto(
      settings.IsPublicRegistrationEnabled,
      settings.DisableDesktopPreview);
  }
}
