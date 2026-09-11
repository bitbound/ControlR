using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using UGDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IUserGroupsApi
{
  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}/{{userGroupId}}/members?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> AddUserGroupMembers(Guid userGroupId, Guid tenantId, UGDtos.AddUserGroupMembersRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<UGDtos.UserGroupDetailDto>> CreateUserGroup(Guid tenantId, UGDtos.CreateUserGroupRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}/{{userGroupId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteUserGroup(Guid userGroupId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<UGDtos.UserGroupsResponseDto>> GetAllUserGroups(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}/{{userGroupId}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<UGDtos.UserGroupDetailDto>> GetUserGroup(Guid userGroupId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}/{{userGroupId}}/members?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> RemoveUserGroupMembers(Guid userGroupId, Guid tenantId, UGDtos.RemoveUserGroupMembersRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserGroupsEndpoint}/{{userGroupId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<UGDtos.UserGroupDetailDto>> UpdateUserGroup(Guid userGroupId, Guid tenantId, UGDtos.UpdateUserGroupRequestDto request, CancellationToken cancellationToken = default);
}