using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V0;

[Route(HttpConstants.V0.LogonTokensEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
[ApiVersion(ApiVersions.V0)]
public class LogonTokensController : ControllerBase
{
  [HttpPost("external")]
  [ProducesResponseType<V0Dtos.LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<V0Dtos.LogonTokenResponseDto>> CreateForExternal(
    [FromServices] AppDb appDb,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromBody] V0Dtos.CreateLogonTokenForExternalRequestDto request)
  {
    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != request.TenantId)
    {
      return BadRequest("Device not found");
    }

    var result = await logonTokenProvider.CreateTokenForExternal(
      request.DeviceId,
      request.TenantId,
      request.UserCorrelationId,
      request.ExpirationMinutes);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(BuildResponse(result.Value));
  }

  [HttpPost("user")]
  [ProducesResponseType<V0Dtos.LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<V0Dtos.LogonTokenResponseDto>> CreateForUser(
    [FromServices] AppDb appDb,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromBody] V0Dtos.CreateLogonTokenForUserRequestDto request)
  {
    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != request.TenantId)
    {
      return BadRequest("Device not found");
    }

    var result = await logonTokenProvider.CreateToken(
      request.DeviceId,
      request.TenantId,
      request.UserId,
      request.ExpirationMinutes);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(BuildResponse(result.Value));
  }

  private V0Dtos.LogonTokenResponseDto BuildResponse(LogonTokenModel logonToken)
  {
    var deviceAccessUrl = new Uri(
      Request.ToOrigin(),
      $"/device-access?deviceId={logonToken.DeviceId}&logonToken={logonToken.Token}");

    return new V0Dtos.LogonTokenResponseDto(
      DeviceAccessUrl: deviceAccessUrl,
      ExpiresAt: logonToken.ExpiresAt,
      Token: logonToken.Token);
  }
}
