using Asp.Versioning;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectiveUserPreferences;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// The calling user's effective preferences, where tenant setting overrides beat user
/// preferences.
/// </summary>
[Route(HttpConstants.V1.EffectiveUserPreferencesEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class EffectiveUserPreferencesController : ControllerBase
{
  [HttpGet]
  [ProducesResponseType<EffectiveUserPreferencesDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<EffectiveUserPreferencesDto>> GetAll(
    [FromServices] IEffectiveUserPreferencesResolver effectiveUserPreferencesResolver,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var preferences = await effectiveUserPreferencesResolver.GetEffectiveUserPreferences(
      resolvedTenantId,
      userId,
      cancellationToken);

    return Ok(new EffectiveUserPreferencesDto(
      preferences.NotifyUserOnSessionStart,
      preferences.IsNotifyUserOnSessionStartTenantEnforced));
  }
}
