using Asp.Versioning;
using ControlR.Web.Client;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Extensions.Dtos.V1;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using ControlR.Web.Server.Constants;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.LogonTokensEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class LogonTokensController : ControllerBase
{
  private const string NoTrustworthyOriginDetail =
    "This server has no public URL configured, so it cannot build the device access URL. " +
    "Set AppOptions:PublicBaseUrl to this server's public URL.";

  [HttpPost("external")]
  [ProducesResponseType<V1Dtos.LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  public async Task<ActionResult<V1Dtos.LogonTokenResponseDto>> CreateForExternal(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogonTokenScopeService logonTokenScopeService,
    [FromServices] IPublicUrlProvider publicUrlProvider,
    [FromBody] V1Dtos.CreateLogonTokenForExternalRequestDto request)
  {
    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != request.TenantId)
    {
      return Problem(
        detail: "Device not found",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    // Redundant while the device is loaded through the filtered AppDb, since a tenant-bound
    // caller can only see its own tenant. Kept because it becomes live on an IgnoreQueryFilters load.
    if (!User.IsServerPrincipal() &&
      (!User.TryGetTenantId(out var callerTenantId) || callerTenantId != device.TenantId))
    {
      return Problem(
        detail: "Device not found",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.LogonTokenCreate);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var creator = User.ToPrincipalDescriptor();
    if (creator is null)
    {
      return Problem(
        detail: "Caller principal not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    // The access URL is opened by someone else's browser, so it comes only from the configured
    // public origin. Resolve it before minting the token, so a server without one refuses instead of
    // minting a token whose URL cannot be built.
    var deviceAccessBaseUrl = publicUrlProvider.TryGetAbsoluteUrl(ClientRoutes.DeviceAccess);
    if (deviceAccessBaseUrl is null)
    {
      return Problem(
        detail: NoTrustworthyOriginDetail,
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: V1ProblemTitles.ServiceUnavailable);
    }

    var result = await logonTokenScopeService.CreateTokenWithScopes(
      request.ToCreationRequest(),
      creator,
      HttpContext.RequestAborted);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(BuildResponse(result.Value, deviceAccessBaseUrl));
  }

  [HttpPost("user")]
  [ProducesResponseType<V1Dtos.LogonTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  public async Task<ActionResult<V1Dtos.LogonTokenResponseDto>> CreateForUser(
    [FromServices] AppDb appDb,
    [FromServices] IAuthorizationService authorizationService,
    [FromServices] ILogonTokenScopeService logonTokenScopeService,
    [FromServices] IPublicUrlProvider publicUrlProvider,
    [FromBody] V1Dtos.CreateLogonTokenForUserRequestDto request)
  {
    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != request.TenantId)
    {
      return Problem(
        detail: "Device not found",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    // Redundant while the device is loaded through the filtered AppDb, since a tenant-bound
    // caller can only see its own tenant. Kept because it becomes live on an IgnoreQueryFilters load.
    if (!User.IsServerPrincipal() &&
      (!User.TryGetTenantId(out var callerTenantId) || callerTenantId != device.TenantId))
    {
      return Problem(
        detail: "Device not found",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var authResult = await authorizationService.AuthorizeAsync(User, device, DeviceResourcePolicies.LogonTokenCreate);
    if (!authResult.Succeeded)
    {
      return Forbid();
    }

    var creator = User.ToPrincipalDescriptor();
    if (creator is null)
    {
      return Problem(
        detail: "Caller principal not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    // Resolved before the token is minted, so a server without a public origin leaves nothing behind.
    var deviceAccessBaseUrl = publicUrlProvider.TryGetAbsoluteUrl(ClientRoutes.DeviceAccess);
    if (deviceAccessBaseUrl is null)
    {
      return Problem(
        detail: NoTrustworthyOriginDetail,
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: V1ProblemTitles.ServiceUnavailable);
    }

    var result = await logonTokenScopeService.CreateTokenWithScopes(
      request.ToCreationRequest(),
      creator,
      HttpContext.RequestAborted);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(BuildResponse(result.Value, deviceAccessBaseUrl));
  }

  private static V1Dtos.LogonTokenResponseDto BuildResponse(
    LogonTokenResult logonToken,
    string deviceAccessBaseUrl)
  {
    var url = QueryHelpers.AddQueryString(
      deviceAccessBaseUrl,
      new Dictionary<string, string?>
      {
        ["deviceId"] = $"{logonToken.DeviceId}",
        ["logonToken"] = logonToken.Token
      });

    return new V1Dtos.LogonTokenResponseDto(
      DeviceAccessUrl: new Uri(url),
      ExpiresAt: logonToken.ExpiresAt,
      Token: logonToken.Token);
  }
}
