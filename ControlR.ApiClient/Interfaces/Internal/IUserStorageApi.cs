using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserStorageApi
{
  [ApiRoute($"{HttpConstants.Internal.UserStorageEndpoint}/{{key}}", "DELETE")]
  [Obsolete("Use ControlrApi.V1.UserStorage.DeleteUserStorageItem (DELETE /api/v1/user-storage/{key}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> DeleteUserStorageItem(string key, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserStorageEndpoint}/{{key}}", "GET")]
  [Obsolete("Use ControlrApi.V1.UserStorage.GetUserStorageItem (GET /api/v1/user-storage/{key}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<UserStorageResponseDto>> GetUserStorageItem(string key, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserStorageEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.UserStorage.SetUserStorageItem (POST /api/v1/user-storage?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<UserStorageResponseDto>> SetUserStorageItem(UserStorageRequestDto request, CancellationToken cancellationToken = default);
}
