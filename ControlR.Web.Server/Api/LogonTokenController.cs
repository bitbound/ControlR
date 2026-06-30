using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Extensions;
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
    [FromServices] IAuthorizationService authorizationService,
    [FromBody] LogonTokenRequestDto request)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return BadRequest("User tenant not found");
      tenantId = tid;
    }

    if (!User.TryGetUserId(out var userId))
    {
      return BadRequest("User ID not found");
    }

    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null)
    {
      return BadRequest("Device not found");
    }

    if (!User.IsServerPrincipal() && device.TenantId != tenantId!.Value)
    {
      return BadRequest("Device not found");
    }

    // Validate that the user exists and belongs to the same tenant

    var user = await appDb.Users.FindAsync(userId);
    if (user is null)
    {
      return BadRequest("User not found or does not belong to this tenant");
    }

    if (!User.IsServerPrincipal() && user.TenantId != tenantId!.Value)
    {
      return BadRequest("User not found or does not belong to this tenant");
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var logonToken = await logonTokenProvider.CreateTokenAsync(
      request.DeviceId,
      tenantId!.Value,
      userId,
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
