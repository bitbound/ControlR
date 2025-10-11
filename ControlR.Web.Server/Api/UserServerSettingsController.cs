using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.UserServerSettingsEndpoint)]
[ApiController]
[Authorize]
[OutputCache(Duration = 60)]
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
}