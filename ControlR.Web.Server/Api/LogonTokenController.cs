using ControlR.Libraries.Shared.Constants;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.LogonTokensEndpoint)]
[ApiController]
[Authorize]
public class LogonTokenController(
  AppDb appDb,
  ILogonTokenProvider logonTokenProvider) : ControllerBase
{
  private readonly ILogonTokenProvider _logonTokenProvider = logonTokenProvider;
  private readonly AppDb _appDb = appDb;

  [HttpPost]
  public async Task<ActionResult<LogonTokenResponseDto>> CreateLogonToken([FromBody] LogonTokenRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found");
    }

    var device = await _appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != tenantId)
    {
      return BadRequest("Device not found");
    }

    // Validate that the user exists and belongs to the same tenant
    var user = await _appDb.Users.FindAsync(request.UserId);
    if (user is null || user.TenantId != tenantId)
    {
      return BadRequest("User not found or does not belong to this tenant");
    }

    var logonToken = await _logonTokenProvider.CreateTokenAsync(
      request.DeviceId,
      tenantId,
      request.UserId,
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
