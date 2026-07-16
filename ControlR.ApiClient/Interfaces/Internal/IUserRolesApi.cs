using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserRolesApi
{
  [ApiRoute("POST", "/api/internal/user-roles")]
  Task<ApiResult> AddUserRole(UserRoleAddRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/user-roles")]
  Task<ApiResult<RoleResponseDto[]>> GetOwnRoles(CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/user-roles/{userId}")]
  Task<ApiResult<RoleResponseDto[]>> GetUserRoles(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/internal/user-roles/{userId}/{roleId}")]
  Task<ApiResult> RemoveUserRole(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}
