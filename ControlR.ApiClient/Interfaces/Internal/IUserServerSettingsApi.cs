using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserServerSettingsApi
{
  [ApiRoute($"{HttpConstants.Internal.UserServerSettingsEndpoint}/decommission-status", "GET")]
  Task<ApiResult<DecommissionServerResponseDto>> GetDecommissionStatus(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserServerSettingsEndpoint}/file-upload-max-size", "GET")]
  Task<ApiResult<long>> GetFileUploadMaxSize(CancellationToken cancellationToken = default);
}
