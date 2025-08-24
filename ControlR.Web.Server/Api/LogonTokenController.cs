using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Authn;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("/api/logon-tokens")]
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationSchemeOptions.DefaultScheme)]
public class LogonTokenController(
  ILogonTokenProvider logonTokenProvider,
  UserManager<AppUser> userManager) : ControllerBase
{
  private readonly ILogonTokenProvider _logonTokenProvider = logonTokenProvider;
  private readonly UserManager<AppUser> _userManager = userManager;

  [HttpPost]
  public async Task<ActionResult<LogonTokenResponseDto>> CreateLogonToken([FromBody] LogonTokenRequestDto request)
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null || user.TenantId == Guid.Empty)
    {
      return BadRequest("User tenant not found");
    }

    // Validate the device exists and belongs to the user's tenant
    // TODO: Add device validation here if needed

    var logonToken = await _logonTokenProvider.CreateTokenAsync(
      request.DeviceId,
      user.TenantId.ToString(),
      request.ExpirationMinutes,
      request.UserIdentifier,
      request.DisplayName,
      request.Email);

    var deviceAccessUrl = new Uri(
      Request.Scheme + "://" + Request.Host + 
      $"/device-access?deviceId={request.DeviceId}&logonToken={logonToken.Token}");

    var response = new LogonTokenResponseDto
    {
      Token = logonToken.Token,
      DeviceAccessUrl = deviceAccessUrl,
      ExpiresAt = logonToken.ExpiresAt
    };

    return Ok(response);
  }
}
