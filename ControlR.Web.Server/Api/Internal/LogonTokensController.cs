using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.LogonTokensEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class LogonTokensController : ControllerBase
{
  [HttpPost]
  [ProducesResponseType<InternalDtos.LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<InternalDtos.LogonTokenResponseDto>> CreateLogonToken(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogonTokenScopeService logonTokenScopeService,
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
    if (device is null || device.TenantId != tenantId)
    {
      return BadRequest("Device not found.");
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.LogonTokenCreate);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var creator = User.ToPrincipalDescriptor();
    if (creator is null)
    {
      return BadRequest("User principal not found.");
    }

    var result = await logonTokenScopeService.CreateTokenWithScopes(
      LogonTokenCreationRequest.From(request, tenantId, userId), creator, HttpContext.RequestAborted);

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
