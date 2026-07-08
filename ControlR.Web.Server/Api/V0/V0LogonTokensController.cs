using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V0;

[Route(HttpConstants.V0.LogonTokensEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
public class V0LogonTokensController : ControllerBase
{
  [HttpPost]
  public async Task<ActionResult<LogonTokenResponseDto>> Create(
    [FromServices] AppDb appDb,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromBody] IssueLogonTokenRequestDto request)
  {
    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != request.TenantId)
    {
      return BadRequest("Device not found");
    }

    if (request.Kind == LogonTokenKind.User)
    {
      if (!request.UserId.HasValue)
      {
        return BadRequest("UserId is required for user logon tokens.");
      }

      var user = await appDb.Users.FindAsync(request.UserId.Value);
      if (user is null || user.TenantId != request.TenantId)
      {
        return BadRequest("User not found or does not belong to this tenant.");
      }
    }

    var logonToken = await logonTokenProvider.CreateTokenAsync(
      request.DeviceId,
      request.TenantId,
      request.UserId,
      request.Kind,
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
