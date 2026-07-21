using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

namespace ControlR.ApiClient.Interfaces.V1;

public interface ITenantsApi
{
  [ApiRoute($"{HttpConstants.V1.TenantsEndpoint}", "POST")]
  Task<ApiResult<CreateTenantResponseDto>> CreateTenant(CreateTenantRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V1.TenantsEndpoint}/{{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteTenant(Guid tenantId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V1.TenantsEndpoint}/{{tenantId}}", "GET")]
  Task<ApiResult<GetTenantResponseDto>> GetTenant(Guid tenantId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V1.TenantsEndpoint}/{{tenantId}}", "PUT")]
  Task<ApiResult<GetTenantResponseDto>> UpdateTenant(Guid tenantId, UpdateTenantRequestDto request, CancellationToken cancellationToken = default);
}
