using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserServerSettings;

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
  [ProducesResponseType<DecommissionServerResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public ActionResult<DecommissionServerResponseDto> GetDecommissionStatus(
    [FromServices] IOptionsMonitor<ServerLifecycleOptions> serverLifecycleOptions)
  {
    var isEnabled = serverLifecycleOptions.CurrentValue.DecommissionServer;
    return Ok(new DecommissionServerResponseDto(isEnabled));
  }

  [HttpGet("file-upload-max-size")]
  [ProducesResponseType<FileUploadMaxSizeResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public ActionResult<FileUploadMaxSizeResponseDto> GetFileUploadMaxSize(
    [FromServices] IOptionsMonitor<AppOptions> appOptions)
  {
    var maxFileSize = appOptions.CurrentValue.MaxFileTransferSize;
    return Ok(new FileUploadMaxSizeResponseDto(maxFileSize));
  }
}
