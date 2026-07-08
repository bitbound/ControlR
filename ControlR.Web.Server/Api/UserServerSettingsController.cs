using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.UserServerSettingsEndpoint)]
[ApiController]
[Authorize]
[OutputCache(Duration = 30)]
public class UserServerSettingsController : ControllerBase
{
  [HttpGet("file-upload-max-size")]
  public ActionResult<FileUploadMaxSizeResponseDto> Get(
    [FromServices] IOptionsMonitor<AppOptions> appOptions
  )
  {
    var maxFileSize = appOptions.CurrentValue.MaxFileTransferSize;
    return Ok(new FileUploadMaxSizeResponseDto(maxFileSize));
  }

  [HttpGet("decommission-status")]
  public ActionResult<DecommissionServerResponseDto> GetDecommissionStatus(
    [FromServices] IOptionsMonitor<ServerLifecycleOptions> serverLifecycleOptions
  )
  {
    var isEnabled = serverLifecycleOptions.CurrentValue.DecommissionServer;
    return Ok(new DecommissionServerResponseDto(isEnabled));
  }
}
