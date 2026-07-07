using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Services.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.TenantsEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
public class V1TenantsController(ITenantProvisioningService tenantProvisioningService) : ControllerBase
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
