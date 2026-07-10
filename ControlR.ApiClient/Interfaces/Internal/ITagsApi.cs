using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITagsApi
{
  Task<ApiResult<TagResponseDto>> CreateTag(TagCreateRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteTag(Guid tagId, CancellationToken cancellationToken = default);
  Task<ApiResult<TagResponseDto[]>> GetAllTags(bool includeLinkedIds = false, CancellationToken cancellationToken = default);
  Task<ApiResult<TagResponseDto>> RenameTag(TagRenameRequestDto request, CancellationToken cancellationToken = default);
}
