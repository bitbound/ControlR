using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IRolesApi
{
  Task<ApiResult<RoleResponseDto[]>> GetAllRoles(CancellationToken cancellationToken = default);
}
