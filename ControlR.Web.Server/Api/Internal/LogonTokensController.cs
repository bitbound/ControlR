using ControlR.Web.Client;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Extensions.Dtos.Internal;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.LogonTokensEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class LogonTokensController : ControllerBase
{
  [HttpPost]
  [ApiDeprecated("/api/v1/logon-tokens/user", Note = "The replacement requires TenantId in the request body; see also /api/v1/logon-tokens/external for server-scoped callers.")]
  [ProducesResponseType<InternalDtos.LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<InternalDtos.LogonTokenResponseDto>> CreateLogonToken(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogonTokenScopeService logonTokenScopeService,
    [FromServices] IPublicUrlProvider publicUrlProvider,
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

    // The access URL is opened by someone else's browser, so it comes only from the configured
    // public origin. Resolve it before minting the token, so a server without one refuses instead of
    // minting a token whose URL cannot be built.
    var deviceAccessBaseUrl = publicUrlProvider.TryGetAbsoluteUrl(ClientRoutes.DeviceAccess);
    if (deviceAccessBaseUrl is null)
    {
      return StatusCode(
        StatusCodes.Status503ServiceUnavailable,
        "This server has no public URL configured, so it cannot build the device access URL. " +
        "Set AppOptions:PublicBaseUrl to this server's public URL.");
    }

    var result = await logonTokenScopeService.CreateTokenWithScopes(
      request.ToCreationRequest(tenantId, userId), creator, HttpContext.RequestAborted);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    var url = QueryHelpers.AddQueryString(
      deviceAccessBaseUrl,
      new Dictionary<string, string?>
      {
        ["deviceId"] = $"{request.DeviceId}",
        ["logonToken"] = result.Value.Token
      });

    var response = new InternalDtos.LogonTokenResponseDto(
      DeviceAccessUrl: new Uri(url),
      ExpiresAt: result.Value.ExpiresAt,
      Token: result.Value.Token);

    return Ok(response);
  }
}
