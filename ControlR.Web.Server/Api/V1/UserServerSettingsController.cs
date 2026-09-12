using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using UserSettingsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserServerSettings;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Client-environment capability probes: values a signed-in client reads to size its requests
/// and adapt its UI.
/// </summary>
[Route(HttpConstants.V1.UserServerSettingsEndpoint)]
[ApiController]
[Authorize]
[OutputCache(Duration = 30)]
[ApiVersion(ApiVersions.V1)]
public class UserServerSettingsController : ControllerBase
{
  [HttpGet("decommission-status")]
  [ProducesResponseType<UserSettingsDtos.DecommissionServerResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public ActionResult<UserSettingsDtos.DecommissionServerResponseDto> GetDecommissionStatus(
    [FromServices] IOptionsMonitor<ServerLifecycleOptions> serverLifecycleOptions)
  {
    var isEnabled = serverLifecycleOptions.CurrentValue.DecommissionServer;
    return Ok(new UserSettingsDtos.DecommissionServerResponseDto(isEnabled));
  }

  [HttpGet("file-upload-max-size")]
  [ProducesResponseType<UserSettingsDtos.FileUploadMaxSizeResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public ActionResult<UserSettingsDtos.FileUploadMaxSizeResponseDto> GetFileUploadMaxSize(
    [FromServices] IOptionsMonitor<AppOptions> appOptions)
  {
    var maxFileSize = appOptions.CurrentValue.MaxFileTransferSize;
    return Ok(new UserSettingsDtos.FileUploadMaxSizeResponseDto(maxFileSize));
  }
}
