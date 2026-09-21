using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IServerAlertApi
{
  [ApiRoute($"{HttpConstants.Internal.ServerAlertEndpoint}", "GET")]
  [Obsolete("Use ControlrApi.V1.ServerAlert.GetServerAlert (GET /api/v1/server-alert), which returns the same value. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<ServerAlertResponseDto>> GetServerAlert(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.ServerAlertEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.ServerAlert.UpdateServerAlert (POST /api/v1/server-alert), which returns the same value. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<ServerAlertResponseDto>> UpdateServerAlert(ServerAlertRequestDto request, CancellationToken cancellationToken = default);
}
