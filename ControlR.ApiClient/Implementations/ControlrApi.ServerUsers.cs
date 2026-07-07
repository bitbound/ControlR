using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<AdminResetPasswordResponseDto>> IServerUsersApi.AdminResetPassword(Guid userId, ServerAdminResetPasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.UsersEndpoint}/server/{userId}/reset-password", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<AdminResetPasswordResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<UserResponseDto>> IServerUsersApi.Create(ServerCreateUserRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.UsersEndpoint}/server", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserResponseDto>(cancellationToken);
    });
  }
}
