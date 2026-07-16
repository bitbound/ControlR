using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserStorageApi
{
  [ApiRoute($"{HttpConstants.Internal.UserStorageEndpoint}/{{key}}", "DELETE")]
  Task<ApiResult> DeleteUserStorageItem(string key, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserStorageEndpoint}/{{key}}", "GET")]
  Task<ApiResult<UserStorageResponseDto>> GetUserStorageItem(string key, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserStorageEndpoint}", "POST")]
  Task<ApiResult<UserStorageResponseDto>> SetUserStorageItem(UserStorageRequestDto request, CancellationToken cancellationToken = default);
}
