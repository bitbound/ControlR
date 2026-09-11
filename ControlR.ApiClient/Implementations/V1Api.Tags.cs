using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using TagsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Tags;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<TagsDtos.TagResponseDto>> ITagsApi.CreateTag(Guid tenantId, TagsDtos.TagCreateRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.TagsEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TagsDtos.TagResponseDto>(cancellationToken);
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

  async Task<ApiResult<TagsDtos.TagsResponseDto>> ITagsApi.GetAllTags(Guid tenantId, bool includeLinkedIds, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<TagsDtos.TagsResponseDto>(
        $"{HttpConstants.V1.TagsEndpoint}?tenantId={tenantId}&includeLinkedIds={includeLinkedIds}",
        cancellationToken));
  }

  async Task<ApiResult<TagsDtos.TagResponseDto>> ITagsApi.GetTag(Guid tagId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<TagsDtos.TagResponseDto>(
        $"{HttpConstants.V1.TagsEndpoint}/{tagId}?tenantId={tenantId}",
        cancellationToken));
  }

  async Task<ApiResult<TagsDtos.TagResponseDto>> ITagsApi.UpdateTag(Guid tagId, Guid tenantId, TagsDtos.UpdateTagRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.TagsEndpoint}/{tagId}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TagsDtos.TagResponseDto>(cancellationToken);
    });
  }
}
