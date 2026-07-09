using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult> IUserStorageApi.DeleteUserStorageItem(string key, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.Internal.UserStorageEndpoint}/{key}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return;
    });
  }

  async Task<ApiResult<UserStorageResponseDto>> IUserStorageApi.GetUserStorageItem(string key, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.Internal.UserStorageEndpoint}/{key}", cancellationToken);
      if (response.StatusCode == HttpStatusCode.NoContent)
      {
        return new UserStorageResponseDto(key, null);
      }

      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserStorageResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.", null, response.StatusCode);
    });
  }

  async Task<ApiResult<UserStorageResponseDto>> IUserStorageApi.SetUserStorageItem(UserStorageRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.UserStorageEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserStorageResponseDto>(cancellationToken);
    });
  }
}
