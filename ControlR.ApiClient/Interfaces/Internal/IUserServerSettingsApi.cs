using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserServerSettingsApi
{
  Task<ApiResult<DecommissionServerResponseDto>> GetDecommissionStatus(CancellationToken cancellationToken = default);
  Task<ApiResult<long>> GetFileUploadMaxSize(CancellationToken cancellationToken = default);
}
