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
  [ProducesResponseType<CreateTenantResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<CreateTenantResponseDto>> Create(
    [FromBody] CreateTenantRequestDto request,
    CancellationToken cancellationToken)
  {
    var result = await tenantProvisioningService.CreateTenant(request, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return CreatedAtAction(nameof(Get), new { id = result.Value.TenantId }, result.Value);
  }

  [HttpDelete("{id:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult> Delete(
    [FromRoute] Guid id,
    CancellationToken cancellationToken)
  {
    var result = await tenantProvisioningService.DeleteTenant(id, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType<GetTenantResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<GetTenantResponseDto>> Get(
    [FromRoute] Guid id,
    CancellationToken cancellationToken)
  {
    var result = await tenantProvisioningService.GetTenant(id, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return Ok(result.Value);
  }

  [HttpPut("{id:guid}")]
  [ProducesResponseType<GetTenantResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<GetTenantResponseDto>> Update(
    [FromRoute] Guid id,
    [FromBody] UpdateTenantRequestDto request,
    CancellationToken cancellationToken)
  {
    var result = await tenantProvisioningService.UpdateTenant(id, request, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return Ok(result.Value);
  }
}
