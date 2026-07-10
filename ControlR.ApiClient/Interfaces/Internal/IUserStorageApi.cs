using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserStorageApi
{
  Task<ApiResult> DeleteUserStorageItem(string key, CancellationToken cancellationToken = default);
  Task<ApiResult<UserStorageResponseDto>> GetUserStorageItem(string key, CancellationToken cancellationToken = default);
  Task<ApiResult<UserStorageResponseDto>> SetUserStorageItem(UserStorageRequestDto request, CancellationToken cancellationToken = default);
}
