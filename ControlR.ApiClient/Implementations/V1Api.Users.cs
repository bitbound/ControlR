using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using PATDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;
using UsersDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<UsersDtos.AdminResetPasswordResponseDto>> IUsersApi.AdminResetPassword(Guid userId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsync(
        $"{HttpConstants.V1.UsersEndpoint}/{userId}/reset-password?tenantId={tenantId}", content: null, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UsersDtos.AdminResetPasswordResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<UsersDtos.UserResponseDto>> IUsersApi.CreateUser(Guid tenantId, UsersDtos.CreateUserRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.UsersEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UsersDtos.UserResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<PATDtos.CreatePersonalAccessTokenResponseDto>> IUsersApi.CreateUserPersonalAccessToken(Guid userId, Guid tenantId, PATDtos.CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.UsersEndpoint}/{userId}/personal-access-tokens?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PATDtos.CreatePersonalAccessTokenResponseDto>(cancellationToken);
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

  async Task<ApiResult<UsersDtos.UsersResponseDto>> IUsersApi.GetAllUsers(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<UsersDtos.UsersResponseDto>(
        $"{HttpConstants.V1.UsersEndpoint}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<PATDtos.PersonalAccessTokenResponseDto[]>> IUsersApi.GetUserPersonalAccessTokens(Guid userId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PATDtos.PersonalAccessTokenResponseDto[]>(
        $"{HttpConstants.V1.UsersEndpoint}/{userId}/personal-access-tokens?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<PATDtos.PersonalAccessTokenResponseDto>> IUsersApi.UpdateUserPersonalAccessToken(Guid userId, Guid tokenId, Guid tenantId, PATDtos.UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.UsersEndpoint}/{userId}/personal-access-tokens/{tokenId}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PATDtos.PersonalAccessTokenResponseDto>(cancellationToken);
    });
  }
}
