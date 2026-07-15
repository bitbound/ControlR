using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<AdminResetPasswordResponseDto>> IUsersApi.AdminResetPassword(Guid userId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.UsersEndpoint}/{userId}/reset-password", new { }, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<AdminResetPasswordResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<UserResponseDto>> IUsersApi.CreateUser(CreateUserRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.UsersEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<CreatePersonalAccessTokenResponseDto>> IUsersApi.CreateUserPersonalAccessToken(Guid userId, CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.UsersEndpoint}/{userId}/personal-access-tokens", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreatePersonalAccessTokenResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IUsersApi.DeleteUser(Guid userId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.Internal.UsersEndpoint}/{userId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IUsersApi.DeleteUserPersonalAccessToken(Guid userId, Guid tokenId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.Internal.UsersEndpoint}/{userId}/personal-access-tokens/{tokenId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<UserResponseDto[]>> IUsersApi.GetAllUsers(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<UserResponseDto[]>(HttpConstants.Internal.UsersEndpoint, cancellationToken));
  }

  async Task<ApiResult<PersonalAccessTokenResponseDto[]>> IUsersApi.GetUserPersonalAccessTokens(Guid userId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PersonalAccessTokenResponseDto[]>($"{HttpConstants.Internal.UsersEndpoint}/{userId}/personal-access-tokens", cancellationToken));
  }

  async Task<ApiResult<PersonalAccessTokenResponseDto>> IUsersApi.UpdateUserPersonalAccessToken(Guid userId, Guid tokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync($"{HttpConstants.Internal.UsersEndpoint}/{userId}/personal-access-tokens/{tokenId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PersonalAccessTokenResponseDto>(cancellationToken);
    });
  }
}
