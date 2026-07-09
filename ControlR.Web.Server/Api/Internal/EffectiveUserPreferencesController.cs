using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.EffectiveUserPreferencesEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class EffectiveUserPreferencesController(IEffectiveUserPreferencesResolver effectiveUserPreferencesResolver) : ControllerBase
{
  private readonly IEffectiveUserPreferencesResolver _effectiveUserPreferencesResolver = effectiveUserPreferencesResolver;

  [HttpGet]
  public async Task<ActionResult<EffectiveUserPreferencesDto>> GetAll(CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId) || !User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var preferences = await _effectiveUserPreferencesResolver.GetEffectiveUserPreferences(
      tenantId,
      userId,
      cancellationToken);

    return Ok(preferences);
  }
}