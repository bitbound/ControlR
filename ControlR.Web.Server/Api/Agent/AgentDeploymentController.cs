using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Agent;

/// <summary>
/// Deployment values the server wants the calling device's install to use. The caller proves it is
/// the device it claims to be by signing the request, so the answer is scoped to that device's own
/// tenant rather than to anything the caller asserts about itself.
/// </summary>
[Route(HttpConstants.Agent.DeploymentOptionsEndpoint)]
[ApiController]
[Authorize(Policy = AgentSignatureAuthenticationSchemeOptions.DefaultPolicy)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class AgentDeploymentController(
  ITenantSettingsManager tenantSettingsManager,
  ILogger<AgentDeploymentController> logger) : ControllerBase
{
  private readonly ILogger<AgentDeploymentController> _logger = logger;
  private readonly ITenantSettingsManager _tenantSettingsManager = tenantSettingsManager;

  [HttpGet]
  [ProducesResponseType<AgentDeploymentOptionsDto>(StatusCodes.Status200OK)]
  public async Task<ActionResult<AgentDeploymentOptionsDto>> Get(CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var settings = await _tenantSettingsManager.GetAllSettings(tenantId, cancellationToken);

    // An instance id only means anything when the tenant has turned the feature on. A value that is
    // unset or disabled is reported as no opinion, not as a directive to drop the instance id, so an
    // install keeps the one it already has.
    var instanceId = settings.AppendInstanceId == true && !string.IsNullOrWhiteSpace(settings.InstanceId)
      ? settings.InstanceId
      : null;

    _logger.LogDebug(
      "Reporting deployment options for tenant {TenantId}. Instance id present: {HasInstanceId}",
      tenantId,
      instanceId is not null);

    return Ok(new AgentDeploymentOptionsDto(instanceId));
  }
}
