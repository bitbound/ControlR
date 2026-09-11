using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeploymentOptions;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization.Capabilities;
using ControlR.Web.Server.Services.Settings;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.DeploymentOptionsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class DeploymentOptionsController(
  IDeviceAuthorizationService deviceAuthorizationService,
  ITenantSettingsManager tenantSettingsManager) : ControllerBase
{
  private readonly IDeviceAuthorizationService _deviceAuthorizationService = deviceAuthorizationService;
  private readonly ITenantSettingsManager _tenantSettingsManager = tenantSettingsManager;

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireAgentInstall)]
  [ProducesResponseType<DeploymentOptionsDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<DeploymentOptionsDto>> Get(
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var settings = await _tenantSettingsManager.GetAllSettings(
      resolvedTenantId,
      cancellationToken);

    return Ok(new DeploymentOptionsDto(
      settings.AppendInstanceId ?? false,
      settings.InstanceId));
  }

  /// <summary>
  /// Returns whether the current principal may assign tags to a prospective deployment target.
  /// The UI uses this to decide whether to offer tag selection. The agent registration endpoint
  /// remains the final enforcement boundary.
  /// </summary>
  [HttpPost("tag-capability")]
  [Authorize(Policy = PolicyNames.RequireAgentInstall)]
  [ProducesResponseType<DeploymentTagCapabilityResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<DeploymentTagCapabilityResponseDto>> GetTagCapability(
    [FromQuery] Guid tenantId,
    [FromBody] DeploymentTagCapabilityRequestDto request,
    [FromServices] UserManager<AppUser> userManager,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.IsServerPrincipal())
    {
      // Server service accounts act as trusted server-wide principals.
      return Ok(new DeploymentTagCapabilityResponseDto(true));
    }

    if (User.ToPrincipalDescriptor() is not { } principal)
    {
      return Unauthorized();
    }

    // The identity-liveness check the superseded internal endpoint performed: a caller whose
    // user record no longer exists (deleted user with a still-valid token) must not receive a
    // capability answer.
    if (await userManager.FindByIdAsync($"{principal.PrincipalId}") is null)
    {
      return Forbid();
    }

    var allowed = await _deviceAuthorizationService.CanAssignTagOnProspectiveDevice(
      principal,
      request.DeviceId,
      request.CustomerId,
      resolvedTenantId,
      cancellationToken);

    return Ok(new DeploymentTagCapabilityResponseDto(allowed));
  }
}