using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserServerSettingsApi
{
  [ApiRoute($"{HttpConstants.Internal.UserServerSettingsEndpoint}/decommission-status", "GET")]
  [Obsolete("Use ControlrApi.V1.UserServerSettings.GetDecommissionStatus (GET /api/v1/user-server-settings/decommission-status), which returns the same value. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<DecommissionServerResponseDto>> GetDecommissionStatus(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserServerSettingsEndpoint}/file-upload-max-size", "GET")]
  Task<ApiResult<long>> GetFileUploadMaxSize(CancellationToken cancellationToken = default);
}
