using ControlR.Libraries.Shared.Constants;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.LogonTokensEndpoint)]
[ApiController]
[Authorize]
public class LogonTokenController : ControllerBase
{

  [HttpPost]
  public async Task<ActionResult<LogonTokenResponseDto>> CreateLogonToken(
    [FromServices] AppDb appDb,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromBody] LogonTokenRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found");
    }

    if (!User.TryGetUserId(out var userId))
    {
      return BadRequest("User ID not found");
    }

    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != tenantId)
    {
      return BadRequest("Device not found");
    }

    // Validate that the user exists and belongs to the same tenant

    var user = await appDb.Users.FindAsync(userId);
    if (user is null || user.TenantId != tenantId)
    {
      return BadRequest("User not found or does not belong to this tenant");
    }

    var logonToken = await logonTokenProvider.CreateTokenAsync(
      request.DeviceId,
      tenantId,
      userId,
      request.ExpirationMinutes);

    var deviceAccessUrl = new Uri(
      Request.ToOrigin(),
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
