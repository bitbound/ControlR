using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Tags;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<TagResponseDto>> ITagsApi.CreateTag(Guid tenantId, TagCreateRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.TagsEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TagResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> ITagsApi.DeleteTag(Guid tagId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.TagsEndpoint}/{tagId}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TagsResponseDto>> ITagsApi.GetAllTags(Guid tenantId, bool includeLinkedIds, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<TagsResponseDto>(
        $"{HttpConstants.V1.TagsEndpoint}?tenantId={tenantId}&includeLinkedIds={includeLinkedIds}",
        cancellationToken));
  }

  async Task<ApiResult<TagResponseDto>> ITagsApi.GetTag(Guid tagId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<TagResponseDto>(
        $"{HttpConstants.V1.TagsEndpoint}/{tagId}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<TagResponseDto>> ITagsApi.UpdateTag(Guid tagId, Guid tenantId, UpdateTagRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.TagsEndpoint}/{tagId}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TagResponseDto>(cancellationToken);
    });
  }
}
