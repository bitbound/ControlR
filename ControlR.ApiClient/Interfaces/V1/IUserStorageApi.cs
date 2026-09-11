using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using StorageDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserStorage;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IUserStorageApi
{
  [ApiRoute($"{HttpConstants.V1.UserStorageEndpoint}/{{key}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteUserStorageItem(string key, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserStorageEndpoint}/{{key}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<StorageDtos.UserStorageResponseDto>> GetUserStorageItem(string key, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserStorageEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<StorageDtos.UserStorageResponseDto>> SetUserStorageItem(Guid tenantId, StorageDtos.UserStorageRequestDto request, CancellationToken cancellationToken = default);
}
