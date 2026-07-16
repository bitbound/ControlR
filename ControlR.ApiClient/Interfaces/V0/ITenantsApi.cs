using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface ITenantsApi
{
  [ApiRoute("POST", "/api/v0/tenants")]
  Task<ApiResult<CreateTenantResponseDto>> CreateTenant(CreateTenantRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/v0/tenants/{tenantId}")]
  Task<ApiResult> DeleteTenant(Guid tenantId, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/v0/tenants/{tenantId}")]
  Task<ApiResult<GetTenantResponseDto>> GetTenant(Guid tenantId, CancellationToken cancellationToken = default);
  [ApiRoute("PUT", "/api/v0/tenants/{tenantId}")]
  Task<ApiResult<GetTenantResponseDto>> UpdateTenant(Guid tenantId, UpdateTenantRequestDto request, CancellationToken cancellationToken = default);
}
