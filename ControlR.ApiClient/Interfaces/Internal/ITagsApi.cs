using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITagsApi
{
  [ApiRoute("POST", "/api/internal/tags")]
  Task<ApiResult<TagResponseDto>> CreateTag(TagCreateRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/internal/tags/{tagId}")]
  Task<ApiResult> DeleteTag(Guid tagId, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/tags")]
  Task<ApiResult<TagResponseDto[]>> GetAllTags(bool includeLinkedIds = false, CancellationToken cancellationToken = default);
  [ApiRoute("PUT", "/api/internal/tags")]
  Task<ApiResult<TagResponseDto>> RenameTag(TagRenameRequestDto request, CancellationToken cancellationToken = default);
}
