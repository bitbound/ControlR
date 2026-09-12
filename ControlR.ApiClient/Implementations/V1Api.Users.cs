using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<AdminResetPasswordResponseDto>> IUsersApi.AdminResetPassword(Guid userId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsync(
        $"{HttpConstants.V1.UsersEndpoint}/{userId}/reset-password?tenantId={tenantId}", content: null, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<AdminResetPasswordResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<UserResponseDto>> IUsersApi.CreateUser(Guid tenantId, CreateUserRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.UsersEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<CreatePersonalAccessTokenResponseDto>> IUsersApi.CreateUserPersonalAccessToken(Guid userId, Guid tenantId, CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.UsersEndpoint}/{userId}/personal-access-tokens?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreatePersonalAccessTokenResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IUsersApi.DeleteUser(Guid userId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.UsersEndpoint}/{userId}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IUsersApi.DeleteUserPersonalAccessToken(Guid userId, Guid tokenId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.UsersEndpoint}/{userId}/personal-access-tokens/{tokenId}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<UsersResponseDto>> IUsersApi.GetAllUsers(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<UsersResponseDto>(
        $"{HttpConstants.V1.UsersEndpoint}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<PersonalAccessTokenResponseDto[]>> IUsersApi.GetUserPersonalAccessTokens(Guid userId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PersonalAccessTokenResponseDto[]>(
        $"{HttpConstants.V1.UsersEndpoint}/{userId}/personal-access-tokens?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<PersonalAccessTokenResponseDto>> IUsersApi.UpdateUserPersonalAccessToken(Guid userId, Guid tokenId, Guid tenantId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.UsersEndpoint}/{userId}/personal-access-tokens/{tokenId}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PersonalAccessTokenResponseDto>(cancellationToken);
    });
  }
}
