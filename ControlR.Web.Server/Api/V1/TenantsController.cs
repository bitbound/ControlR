using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using ControlR.Web.Server.Extensions.Dtos.V1;
using ControlR.Web.Server.Services.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.TenantsEndpoint)]
[ApiController]
[ApiVersion(ApiVersions.V1)]
public class TenantsController(ITenantProvisioningService tenantProvisioningService) : ControllerBase
{
  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireServerTenantsWrite)]
  [ProducesResponseType<CreateTenantResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<CreateTenantResponseDto>> Create(
    [FromBody] CreateTenantRequestDto request,
    CancellationToken cancellationToken)
  {
    var result = await tenantProvisioningService.CreateTenant(request.Name, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult(x => x.ToV1CreateTenantDto());
    }

    return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value.ToV1CreateTenantDto());
  }

  [HttpDelete("{id:guid}")]
  [Authorize(Policy = PolicyNames.RequireServerTenantsDelete)]
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
  [Authorize(Policy = PolicyNames.RequireServerTenantsRead)]
  [ProducesResponseType<GetTenantResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<GetTenantResponseDto>> Get(
    [FromRoute] Guid id,
    CancellationToken cancellationToken)
  {
    var result = await tenantProvisioningService.GetTenant(id, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult(x => x.ToV1GetTenantDto());
    }

    return Ok(result.Value.ToV1GetTenantDto());
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireServerTenantsRead)]
  [ProducesResponseType<TenantsResponseDto>(StatusCodes.Status200OK)]
  public async Task<ActionResult<TenantsResponseDto>> GetAll(
    [FromServices] AppDb appDb,
    CancellationToken cancellationToken)
  {
    var tenants = await appDb.Tenants
      .AsNoTracking()
      .OrderBy(x => x.Name)
      .Select(x => new TenantSummaryDto(x.Id, x.Name ?? string.Empty))
      .ToListAsync(cancellationToken);

    return Ok(new TenantsResponseDto
    {
      Items = [.. tenants]
    });
  }

  [HttpPut("{id:guid}")]
  [Authorize(Policy = PolicyNames.RequireServerTenantsWrite)]
  [ProducesResponseType<GetTenantResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<GetTenantResponseDto>> Update(
    [FromRoute] Guid id,
    [FromBody] UpdateTenantRequestDto request,
    CancellationToken cancellationToken)
  {
    var result = await tenantProvisioningService.UpdateTenant(id, request.Name, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult(x => x.ToV1GetTenantDto());
    }

    return Ok(result.Value.ToV1GetTenantDto());
  }
}
