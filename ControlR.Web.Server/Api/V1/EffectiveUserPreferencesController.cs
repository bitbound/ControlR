using Asp.Versioning;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectiveUserPreferences;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// The calling user's effective preferences: tenant setting overrides beat user preferences.
/// The caller's user id comes from claims. TenantId is required so the read works for the
/// same set of principals as the other V1 surfaces.
/// </summary>
[Route(HttpConstants.V1.EffectiveUserPreferencesEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class EffectiveUserPreferencesController : ControllerBase
{
  [HttpGet]
  [ProducesResponseType<EffectiveUserPreferencesDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
