using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserServerSettingsApi
{
  [ApiRoute("GET", "/api/internal/user-server-settings/decommission-status")]
  Task<ApiResult<DecommissionServerResponseDto>> GetDecommissionStatus(CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/user-server-settings/file-upload-max-size")]
  Task<ApiResult<long>> GetFileUploadMaxSize(CancellationToken cancellationToken = default);
}
