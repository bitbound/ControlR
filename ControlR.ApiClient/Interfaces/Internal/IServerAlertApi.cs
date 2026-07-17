using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IServerAlertApi
{
  [ApiRoute($"{HttpConstants.Internal.ServerAlertEndpoint}", "GET")]
  Task<ApiResult<ServerAlertResponseDto>> GetServerAlert(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.ServerAlertEndpoint}", "POST")]
  Task<ApiResult<ServerAlertResponseDto>> UpdateServerAlert(ServerAlertRequestDto request, CancellationToken cancellationToken = default);
}
