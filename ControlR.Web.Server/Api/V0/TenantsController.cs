using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;
using ControlR.Web.Server.Services.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V0;

[Route(HttpConstants.V0.TenantsEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
[ApiVersion(ApiVersions.V0)]
public class TenantsController(ITenantProvisioningService tenantProvisioningService) : ControllerBase
{
  [HttpPost]
  public async Task<ActionResult<CreateTenantResponseDto>> Create(
    [FromBody] CreateTenantRequestDto request,
    CancellationToken cancellationToken)
  {
    var result = await tenantProvisioningService.CreateTenant(request, cancellationToken);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return CreatedAtAction(nameof(Create), new { id = result.Value.TenantId }, result.Value);
  }
}
