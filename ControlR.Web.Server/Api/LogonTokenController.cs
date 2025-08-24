using ControlR.Libraries.Shared.Constants;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.LogonTokensEndpoint)]
[ApiController]
[Authorize(Roles = RoleNames.TenantAdministrator)]
public class LogonTokenController(
  UserManager<AppUser> userManager,
  AppDb appDb,
  ILogonTokenProvider logonTokenProvider) : ControllerBase
{
  private readonly ILogonTokenProvider _logonTokenProvider = logonTokenProvider;
  private readonly AppDb _appDb = appDb;
  private readonly UserManager<AppUser> _userManager = userManager;

  [HttpPost]
  public async Task<ActionResult<LogonTokenResponseDto>> CreateLogonToken([FromBody] LogonTokenRequestDto request)
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null || user.TenantId == Guid.Empty)
    {
      return BadRequest("User tenant not found");
    }

    var device = await _appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != user.TenantId)
    {
      return BadRequest("Device not found");
    }

    var logonToken = await _logonTokenProvider.CreateTokenAsync(
      request.DeviceId,
      user.TenantId,
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
