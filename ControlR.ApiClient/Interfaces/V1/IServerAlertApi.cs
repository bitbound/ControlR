using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerAlerts;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IServerAlertApi
{
  [ApiRoute($"{HttpConstants.V1.ServerAlertEndpoint}", "GET")]
  Task<ApiResult<ServerAlertResponseDto>> GetServerAlert(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.ServerAlertEndpoint}", "POST")]
  Task<ApiResult<ServerAlertResponseDto>> UpdateServerAlert(
    ServerAlertRequestDto request,
    CancellationToken cancellationToken = default);
}
