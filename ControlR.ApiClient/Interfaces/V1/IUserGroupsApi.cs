using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IUserGroupsApi
{
  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}/{{userGroupId}}/members?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> AddUserGroupMembers(Guid userGroupId, Guid tenantId, AddUserGroupMembersRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<UserGroupDetailDto>> CreateUserGroup(Guid tenantId, CreateUserGroupRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}/{{userGroupId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteUserGroup(Guid userGroupId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<UserGroupsResponseDto>> GetAllUserGroups(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}/{{userGroupId}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<UserGroupDetailDto>> GetUserGroup(Guid userGroupId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}/{{userGroupId}}/members?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> RemoveUserGroupMembers(Guid userGroupId, Guid tenantId, RemoveUserGroupMembersRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}/{{userGroupId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<UserGroupDetailDto>> UpdateUserGroup(Guid userGroupId, Guid tenantId, UpdateUserGroupRequestDto request, CancellationToken cancellationToken = default);
}