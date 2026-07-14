using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.UserServerSettingsEndpoint)]
[Route(HttpConstants.Legacy.UserServerSettingsEndpoint)]
[ApiController]
[Authorize]
[OutputCache(Duration = 30)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class UserServerSettingsController : ControllerBase
{
  [HttpGet("file-upload-max-size")]
  public ActionResult<InternalDtos.FileUploadMaxSizeResponseDto> Get(
    [FromServices] IOptionsMonitor<AppOptions> appOptions
  )
  {
    var maxFileSize = appOptions.CurrentValue.MaxFileTransferSize;
    return Ok(new InternalDtos.FileUploadMaxSizeResponseDto(maxFileSize));
  }

  [HttpGet("decommission-status")]
  public ActionResult<InternalDtos.DecommissionServerResponseDto> GetDecommissionStatus(
    [FromServices] IOptionsMonitor<ServerLifecycleOptions> serverLifecycleOptions
  )
  {
    var isEnabled = serverLifecycleOptions.CurrentValue.DecommissionServer;
    return Ok(new InternalDtos.DecommissionServerResponseDto(isEnabled));
  }
}
