using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface ITenantsApi
{
  [ApiRoute($"{HttpConstants.V0.TenantsEndpoint}", "POST")]
  Task<ApiResult<CreateTenantResponseDto>> CreateTenant(CreateTenantRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.TenantsEndpoint}/{{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteTenant(Guid tenantId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.TenantsEndpoint}/{{tenantId}}", "GET")]
  Task<ApiResult<GetTenantResponseDto>> GetTenant(Guid tenantId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.TenantsEndpoint}/{{tenantId}}", "PUT")]
  Task<ApiResult<GetTenantResponseDto>> UpdateTenant(Guid tenantId, UpdateTenantRequestDto request, CancellationToken cancellationToken = default);
}
