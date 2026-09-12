using Asp.Versioning;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.LogonTokensEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class LogonTokensController : ControllerBase
{
  [HttpPost("external")]
  [ProducesResponseType<V1Dtos.LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<V1Dtos.LogonTokenResponseDto>> CreateForExternal(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogonTokenScopeService logonTokenScopeService,
    [FromBody] V1Dtos.CreateLogonTokenForExternalRequestDto request)
  {
    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != request.TenantId)
    {
      return BadRequest("Device not found");
    }

    // Redundant while the device is loaded through the filtered AppDb, since a tenant-bound
    // caller can only see its own tenant. Kept because it becomes live on an IgnoreQueryFilters load.
    if (!User.IsServerPrincipal() &&
      (!User.TryGetTenantId(out var callerTenantId) || callerTenantId != device.TenantId))
    {
      return BadRequest("Device not found");
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.LogonTokenCreate);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var creator = User.ToPrincipalDescriptor();
    if (creator is null)
    {
      return BadRequest("Caller principal not found.");
    }

    var result = await logonTokenScopeService.CreateTokenWithScopes(
      LogonTokenCreationRequest.From(request), creator, HttpContext.RequestAborted);

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
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<V1Dtos.LogonTokenResponseDto>> CreateForUser(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogonTokenScopeService logonTokenScopeService,
    [FromBody] V1Dtos.CreateLogonTokenForUserRequestDto request)
  {
    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != request.TenantId)
    {
      return BadRequest("Device not found");
    }

    // Redundant while the device is loaded through the filtered AppDb, since a tenant-bound
    // caller can only see its own tenant. Kept because it becomes live on an IgnoreQueryFilters load.
    if (!User.IsServerPrincipal() &&
      (!User.TryGetTenantId(out var callerTenantId) || callerTenantId != device.TenantId))
    {
      return BadRequest("Device not found");
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.LogonTokenCreate);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var creator = User.ToPrincipalDescriptor();
    if (creator is null)
    {
      return BadRequest("Caller principal not found.");
    }

    var result = await logonTokenScopeService.CreateTokenWithScopes(
      LogonTokenCreationRequest.From(request), creator, HttpContext.RequestAborted);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(BuildResponse(result.Value));
  }

  private V1Dtos.LogonTokenResponseDto BuildResponse(LogonTokenResult logonToken)
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
