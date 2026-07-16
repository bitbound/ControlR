using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IRolesApi
{
  [ApiRoute("GET", "/api/internal/roles")]
  Task<ApiResult<RoleResponseDto[]>> GetAllRoles(CancellationToken cancellationToken = default);
}
