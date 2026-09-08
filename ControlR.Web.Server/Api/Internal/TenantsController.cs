using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.TenantsEndpoint)]
[ApiController]
[Authorize(Policy = PolicyNames.RequireServerTenantsRead)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class TenantsController : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<IReadOnlyList<InternalDtos.TenantSummaryDto>>> Get(
    [FromServices] AppDb appDb,
    CancellationToken cancellationToken)
  {
    var tenants = await appDb.Tenants
      .AsNoTracking()
      .OrderBy(x => x.Name)
      .Select(x => new InternalDtos.TenantSummaryDto(x.Id, x.Name))
      .ToListAsync(cancellationToken);

    return Ok(tenants);
  }
}
