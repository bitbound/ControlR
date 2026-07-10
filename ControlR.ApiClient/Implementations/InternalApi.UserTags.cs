using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult> IUserTagsApi.AddUserTag(UserTagAddRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.UserTagsEndpoint}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TagResponseDto[]>> IUserTagsApi.GetAllowedTags(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<TagResponseDto[]>(HttpConstants.Internal.UserTagsEndpoint, cancellationToken));
  }

  async Task<ApiResult<TagResponseDto[]>> IUserTagsApi.GetUserTags(Guid userId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<TagResponseDto[]>(
        $"{HttpConstants.Internal.UserTagsEndpoint}/{userId}",
        cancellationToken));
  }

  async Task<ApiResult> IUserTagsApi.RemoveUserTag(Guid userId, Guid tagId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.Internal.UserTagsEndpoint}/{userId}/{tagId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
