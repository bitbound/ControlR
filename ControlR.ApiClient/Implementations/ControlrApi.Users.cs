using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<AdminResetPasswordResponseDto>> IUsersApi.AdminResetPassword(Guid userId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.UsersEndpoint}/{userId}/admin-reset-password", new { }, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<AdminResetPasswordResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<UserResponseDto>> IUsersApi.CreateUser(CreateUserRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.UsersEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<CreatePersonalAccessTokenResponseDto>> IUsersApi.CreateUserPersonalAccessToken(Guid userId, CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.UsersEndpoint}/{userId}/personal-access-tokens", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreatePersonalAccessTokenResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IUsersApi.DeleteUser(Guid userId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.UsersEndpoint}/{userId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IUsersApi.DeleteUserPersonalAccessToken(Guid userId, Guid tokenId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.UsersEndpoint}/{userId}/personal-access-tokens/{tokenId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<UserResponseDto[]>> IUsersApi.GetAllUsers(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<UserResponseDto[]>(HttpConstants.UsersEndpoint, cancellationToken));
  }

  async Task<ApiResult<PersonalAccessTokenDto[]>> IUsersApi.GetUserPersonalAccessTokens(Guid userId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<PersonalAccessTokenDto[]>($"{HttpConstants.UsersEndpoint}/{userId}/personal-access-tokens", cancellationToken));
  }

  async Task<ApiResult<PersonalAccessTokenDto>> IUsersApi.UpdateUserPersonalAccessToken(Guid userId, Guid tokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PutAsJsonAsync($"{HttpConstants.UsersEndpoint}/{userId}/personal-access-tokens/{tokenId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PersonalAccessTokenDto>(cancellationToken);
    });
  }
}
