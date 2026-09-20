using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITagsApi
{
  [ApiRoute($"{HttpConstants.Internal.TagsEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.Tags.CreateTag (POST /api/v1/tags?tenantId=), which requires tenantId and returns 201. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<TagResponseDto>> CreateTag(TagCreateRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TagsEndpoint}/{{tagId}}", "DELETE")]
  [Obsolete("Use ControlrApi.V1.Tags.DeleteTag (DELETE /api/v1/tags/{tagId}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> DeleteTag(Guid tagId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TagsEndpoint}", "GET")]
  [Obsolete("Use ControlrApi.V1.Tags.GetAllTags (GET /api/v1/tags?tenantId=), which requires tenantId and returns an Items envelope. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<TagResponseDto[]>> GetAllTags(bool includeLinkedIds = false, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TagsEndpoint}", "PUT")]
  [Obsolete("Use ControlrApi.V1.Tags.UpdateTag (PUT /api/v1/tags/{tagId}?tenantId=), which takes the tag id in the route, requires tenantId, and a body with only name. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<TagResponseDto>> RenameTag(TagRenameRequestDto request, CancellationToken cancellationToken = default);
}
