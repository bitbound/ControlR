using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult> IUserRolesApi.AddUserRole(UserRoleAddRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.UserRolesEndpoint}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<RoleResponseDto[]>> IUserRolesApi.GetOwnRoles(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<RoleResponseDto[]>(HttpConstants.Internal.UserRolesEndpoint, cancellationToken));
  }

  async Task<ApiResult<RoleResponseDto[]>> IUserRolesApi.GetUserRoles(Guid userId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<RoleResponseDto[]>($"{HttpConstants.Internal.UserRolesEndpoint}/{userId}", cancellationToken));
  }

  async Task<ApiResult> IUserRolesApi.RemoveUserRole(Guid userId, Guid roleId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.Internal.UserRolesEndpoint}/{userId}/{roleId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
