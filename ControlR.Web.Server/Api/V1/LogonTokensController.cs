using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.LogonTokensEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
[ApiVersion(ApiVersions.V1)]
public class LogonTokensController : ControllerBase
{
  [HttpPost("external")]
  [ProducesResponseType<V1Dtos.LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<V1Dtos.LogonTokenResponseDto>> CreateForExternal(
    [FromServices] AppDb appDb,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromBody] V1Dtos.CreateLogonTokenForExternalRequestDto request)
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
       request.ExpirationMinutes,
       userDisplayName: request.UserDisplayName,
       sessionCorrelationId: request.SessionCorrelationId);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(BuildResponse(result.Value));
  }

  [HttpPost("user")]
  [ProducesResponseType<V1Dtos.LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<V1Dtos.LogonTokenResponseDto>> CreateForUser(
    [FromServices] AppDb appDb,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromBody] V1Dtos.CreateLogonTokenForUserRequestDto request)
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
      request.ExpirationMinutes,
      sessionCorrelationId: request.SessionCorrelationId);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(BuildResponse(result.Value));
  }

  private V1Dtos.LogonTokenResponseDto BuildResponse(LogonTokenModel logonToken)
  {
    var deviceAccessUrl = new Uri(
      Request.ToOrigin(),
      $"/device-access?deviceId={logonToken.DeviceId}&logonToken={logonToken.Token}");

    return new V1Dtos.LogonTokenResponseDto(
      DeviceAccessUrl: deviceAccessUrl,
      ExpiresAt: logonToken.ExpiresAt,
      Token: logonToken.Token);
  }
}
