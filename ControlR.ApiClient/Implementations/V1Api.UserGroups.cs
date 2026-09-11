using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using UGDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult> IUserGroupsApi.AddUserGroupMembers(Guid userGroupId, Guid tenantId, UGDtos.AddUserGroupMembersRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.UserGroupsEndpoint}/{userGroupId}/members?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<UGDtos.UserGroupDetailDto>> IUserGroupsApi.CreateUserGroup(Guid tenantId, UGDtos.CreateUserGroupRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.UserGroupsEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UGDtos.UserGroupDetailDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IUserGroupsApi.DeleteUserGroup(Guid userGroupId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.UserGroupsEndpoint}/{userGroupId}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<UGDtos.UserGroupsResponseDto>> IUserGroupsApi.GetAllUserGroups(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<UGDtos.UserGroupsResponseDto>(
        $"{HttpConstants.V1.UserGroupsEndpoint}?tenantId={tenantId}", cancellationToken));
  }

  async Task<ApiResult<UGDtos.UserGroupDetailDto>> IUserGroupsApi.GetUserGroup(Guid userGroupId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<UGDtos.UserGroupDetailDto>(
        $"{HttpConstants.V1.UserGroupsEndpoint}/{userGroupId}?tenantId={tenantId}", cancellationToken));
  }

  async Task<ApiResult> IUserGroupsApi.RemoveUserGroupMembers(Guid userGroupId, Guid tenantId, UGDtos.RemoveUserGroupMembersRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
        $"{HttpConstants.V1.UserGroupsEndpoint}/{userGroupId}/members?tenantId={tenantId}")
      {
        Content = JsonContent.Create(request)
      }, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<UGDtos.UserGroupDetailDto>> IUserGroupsApi.UpdateUserGroup(Guid userGroupId, Guid tenantId, UGDtos.UpdateUserGroupRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.UserGroupsEndpoint}/{userGroupId}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UGDtos.UserGroupDetailDto>(cancellationToken);
    });
  }
}