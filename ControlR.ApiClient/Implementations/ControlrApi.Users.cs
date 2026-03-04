using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<UserResponseDto>> IUsersApi.CreateUser(CreateUserRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.UsersEndpoint, request, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<UserResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IUsersApi.DeleteUser(Guid userId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.UsersEndpoint}/{userId}", cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult<UserResponseDto[]>> IUsersApi.GetAllUsers(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<UserResponseDto[]>(HttpConstants.UsersEndpoint, cancellationToken));
  }
}
