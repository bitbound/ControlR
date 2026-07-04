using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> IUserStorageApi.DeleteUserStorageItem(string key, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.UserStorageEndpoint}/{key}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return;
    });
  }

  async Task<ApiResult<UserStorageResponseDto>> IUserStorageApi.GetUserStorageItem(string key, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.UserStorageEndpoint}/{key}", cancellationToken);
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
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.UserStorageEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<UserStorageResponseDto>(cancellationToken);
    });
  }
}
