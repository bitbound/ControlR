using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> IUserRolesApi.AddUserRole(UserRoleAddRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.UserRolesEndpoint}", request, cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult<RoleResponseDto[]>> IUserRolesApi.GetOwnRoles(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<RoleResponseDto[]>(HttpConstants.UserRolesEndpoint, cancellationToken));
  }

  async Task<ApiResult<RoleResponseDto[]>> IUserRolesApi.GetUserRoles(Guid userId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<RoleResponseDto[]>($"{HttpConstants.UserRolesEndpoint}/{userId}", cancellationToken));
  }

  async Task<ApiResult> IUserRolesApi.RemoveUserRole(Guid userId, Guid roleId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.UserRolesEndpoint}/{userId}/{roleId}", cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }
}
