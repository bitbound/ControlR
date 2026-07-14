using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.LogonTokensEndpoint)]
[Route(HttpConstants.Legacy.LogonTokensEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class LogonTokenController : ControllerBase
{
  [HttpPost]
  [ProducesResponseType<InternalDtos.LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<InternalDtos.LogonTokenResponseDto>> CreateLogonToken(
    [FromServices] AppDb appDb,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromServices] IAuthorizationService authorizationService,
    [FromBody] InternalDtos.LogonTokenRequestDto request)
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

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceAccessByDeviceResourcePolicy.PolicyName);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var result = await logonTokenProvider.CreateToken(
      request.DeviceId,
      tenantId,
      userId,
      request.ExpirationMinutes);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    var deviceAccessUrl = new Uri(
      Request.ToOrigin(),
      $"/device-access?deviceId={request.DeviceId}&logonToken={result.Value.Token}");

    var response = new InternalDtos.LogonTokenResponseDto(
      DeviceAccessUrl: deviceAccessUrl,
      ExpiresAt: result.Value.ExpiresAt,
      Token: result.Value.Token);

    return Ok(response);
  }
}
