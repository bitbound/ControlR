using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserStorageApi
{
  [ApiRoute("DELETE", "/api/internal/user-storage/{key}")]
  Task<ApiResult> DeleteUserStorageItem(string key, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/user-storage/{key}")]
  Task<ApiResult<UserStorageResponseDto>> GetUserStorageItem(string key, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/user-storage")]
  Task<ApiResult<UserStorageResponseDto>> SetUserStorageItem(UserStorageRequestDto request, CancellationToken cancellationToken = default);
}
