using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<TagResponseDto>> ITagsApi.CreateTag(TagCreateRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.TagsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TagResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> ITagsApi.DeleteTag(Guid tagId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.Internal.TagsEndpoint}/{tagId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TagResponseDto[]>> ITagsApi.GetAllTags(bool includeLinkedIds, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<TagResponseDto[]>(
        $"{HttpConstants.Internal.TagsEndpoint}?includeLinkedIds={includeLinkedIds}",
        cancellationToken));
  }

  async Task<ApiResult<TagResponseDto>> ITagsApi.RenameTag(TagRenameRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync($"{HttpConstants.Internal.TagsEndpoint}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TagResponseDto>(cancellationToken);
    });
  }
}
