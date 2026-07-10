using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserTagsApi
{
  Task<ApiResult> AddUserTag(UserTagAddRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<TagResponseDto[]>> GetAllowedTags(CancellationToken cancellationToken = default);
  Task<ApiResult<TagResponseDto[]>> GetUserTags(Guid userId, CancellationToken cancellationToken = default);
  Task<ApiResult> RemoveUserTag(Guid userId, Guid tagId, CancellationToken cancellationToken = default);
}
