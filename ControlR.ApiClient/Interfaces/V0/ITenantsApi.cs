using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface ITenantsApi
{
  Task<ApiResult> DeleteTenant(Guid tenantId, CancellationToken cancellationToken = default);
  Task<ApiResult<CreateTenantResponseDto>> CreateTenant(CreateTenantRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<GetTenantResponseDto>> GetTenant(Guid tenantId, CancellationToken cancellationToken = default);
  Task<ApiResult<GetTenantResponseDto>> UpdateTenant(Guid tenantId, UpdateTenantRequestDto request, CancellationToken cancellationToken = default);
}
