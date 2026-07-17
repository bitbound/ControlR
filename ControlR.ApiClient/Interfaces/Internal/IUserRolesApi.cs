using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserRolesApi
{
  [ApiRoute($"{HttpConstants.Internal.UserRolesEndpoint}", "POST")]
  Task<ApiResult> AddUserRole(UserRoleAddRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserRolesEndpoint}", "GET")]
  Task<ApiResult<RoleResponseDto[]>> GetOwnRoles(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserRolesEndpoint}/{{userId}}", "GET")]
  Task<ApiResult<RoleResponseDto[]>> GetUserRoles(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserRolesEndpoint}/{{userId}}/{{roleId}}", "DELETE")]
  Task<ApiResult> RemoveUserRole(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}
