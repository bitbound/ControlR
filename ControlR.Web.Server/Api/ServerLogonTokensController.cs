using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.LogonTokensEndpoint + "/server")]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
public class ServerLogonTokensController : ControllerBase
{
  [HttpPost]
  public async Task<ActionResult<LogonTokenResponseDto>> CreateLogonToken(
    [FromServices] AppDb appDb,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromBody] ServerLogonTokenRequestDto request)
  {
    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != request.TenantId)
    {
      return BadRequest("Device not found");
    }

    var user = await appDb.Users.FindAsync(request.UserId);
    if (user is null || user.TenantId != request.TenantId)
    {
      return BadRequest("User not found or does not belong to this tenant");
    }

    var logonToken = await logonTokenProvider.CreateTokenAsync(
      request.DeviceId,
      request.TenantId,
      request.UserId,
      request.ExpirationMinutes);

    var deviceAccessUrl = new Uri(
      Request.ToOrigin(),
      $"/device-access?deviceId={request.DeviceId}&logonToken={logonToken.Token}");

    var response = new LogonTokenResponseDto(
      DeviceAccessUrl: deviceAccessUrl,
      ExpiresAt: logonToken.ExpiresAt,
      Token: logonToken.Token);

    return Ok(response);
  }
}