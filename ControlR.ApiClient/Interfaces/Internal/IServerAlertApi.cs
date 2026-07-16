using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IServerAlertApi
{
  [ApiRoute("GET", "/api/internal/server-alert")]
  Task<ApiResult<ServerAlertResponseDto>> GetServerAlert(CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/server-alert")]
  Task<ApiResult<ServerAlertResponseDto>> UpdateServerAlert(ServerAlertRequestDto request, CancellationToken cancellationToken = default);
}
