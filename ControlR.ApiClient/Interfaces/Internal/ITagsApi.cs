using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITagsApi
{
  [ApiRoute($"{HttpConstants.Internal.TagsEndpoint}", "POST")]
  Task<ApiResult<TagResponseDto>> CreateTag(TagCreateRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TagsEndpoint}/{{tagId}}", "DELETE")]
  Task<ApiResult> DeleteTag(Guid tagId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TagsEndpoint}", "GET")]
  Task<ApiResult<TagResponseDto[]>> GetAllTags(bool includeLinkedIds = false, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TagsEndpoint}", "PUT")]
  Task<ApiResult<TagResponseDto>> RenameTag(TagRenameRequestDto request, CancellationToken cancellationToken = default);
}
