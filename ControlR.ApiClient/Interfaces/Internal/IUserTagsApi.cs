using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserTagsApi
{
  [ApiRoute($"{HttpConstants.Internal.UserTagsEndpoint}", "POST")]
  Task<ApiResult> AddUserTag(UserTagAddRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserTagsEndpoint}", "GET")]
  Task<ApiResult<TagResponseDto[]>> GetAllowedTags(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserTagsEndpoint}/{{userId}}", "GET")]
  Task<ApiResult<TagResponseDto[]>> GetUserTags(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserTagsEndpoint}/{{userId}}/{{tagId}}", "DELETE")]
  Task<ApiResult> RemoveUserTag(Guid userId, Guid tagId, CancellationToken cancellationToken = default);
}
