using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.LogonTokensEndpoint)]
[ApiController]
[Authorize]
public class LogonTokenController : ControllerBase
{
  /// <summary>Creates a logon token for the authenticated user's device. The token enables device access from the web viewer.</summary>
  [HttpPost]
  [Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
  [ProducesResponseType<LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<LogonTokenResponseDto>> CreateLogonToken(
    [FromServices] AppDb appDb,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromServices] IAuthorizationService authorizationService,
    [FromBody] LogonTokenRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (!User.TryGetUserId(out var userId))
    {
      return BadRequest("User ID not found.");
    }

    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null)
    {
      return BadRequest("Device not found.");
    }

    if (device.TenantId != tenantId)
    {
      return BadRequest("Device not found.");
    }

    var user = await appDb.Users.FindAsync(userId);
    if (user is null)
    {
      return BadRequest("User not found or does not belong to this tenant.");
    }

    if (user.TenantId != tenantId)
    {
      return BadRequest("User not found or does not belong to this tenant.");
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var logonToken = await logonTokenProvider.CreateTokenAsync(
      request.DeviceId,
      tenantId,
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

  /// <summary>Issues a logon token as a server service account. All fields are required; the caller has no user or tenant claims.</summary>
  [HttpPost("issue")]
  [Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
  [ProducesResponseType<LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<ActionResult<LogonTokenResponseDto>> IssueLogonToken(
    [FromServices] AppDb appDb,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromBody] IssueLogonTokenRequestDto request)
  {
    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != request.TenantId)
    {
      return BadRequest("Device not found");
    }

    var user = await appDb.Users.FindAsync(request.UserId);
    if (user is null || user.TenantId != request.TenantId)
    {
      return BadRequest("User not found or does not belong to this tenant.");
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
