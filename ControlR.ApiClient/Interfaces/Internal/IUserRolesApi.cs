using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserRolesApi
{
  Task<ApiResult> AddUserRole(UserRoleAddRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<RoleResponseDto[]>> GetOwnRoles(CancellationToken cancellationToken = default);
  Task<ApiResult<RoleResponseDto[]>> GetUserRoles(Guid userId, CancellationToken cancellationToken = default);
  Task<ApiResult> RemoveUserRole(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}
