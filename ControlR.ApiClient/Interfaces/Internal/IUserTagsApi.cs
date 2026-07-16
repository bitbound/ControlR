using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserTagsApi
{
  [ApiRoute("POST", "/api/internal/user-tags")]
  Task<ApiResult> AddUserTag(UserTagAddRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/user-tags")]
  Task<ApiResult<TagResponseDto[]>> GetAllowedTags(CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/user-tags/{userId}")]
  Task<ApiResult<TagResponseDto[]>> GetUserTags(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/internal/user-tags/{userId}/{tagId}")]
  Task<ApiResult> RemoveUserTag(Guid userId, Guid tagId, CancellationToken cancellationToken = default);
}
