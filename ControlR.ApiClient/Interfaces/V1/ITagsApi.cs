using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using TagsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Tags;

namespace ControlR.ApiClient.Interfaces.V1;

public interface ITagsApi
{
  [ApiRoute($"{HttpConstants.V1.TagsEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<TagsDtos.TagResponseDto>> CreateTag(Guid tenantId, TagsDtos.TagCreateRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TagsEndpoint}/{{tagId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteTag(Guid tagId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TagsEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<TagsDtos.TagsResponseDto>> GetAllTags(Guid tenantId, bool includeLinkedIds = false, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TagsEndpoint}/{{tagId}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<TagsDtos.TagResponseDto>> GetTag(Guid tagId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TagsEndpoint}/{{tagId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<TagsDtos.TagResponseDto>> UpdateTag(Guid tagId, Guid tenantId, TagsDtos.UpdateTagRequestDto request, CancellationToken cancellationToken = default);
}
