using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IServerAlertApi
{
  Task<ApiResult<ServerAlertResponseDto>> GetServerAlert(CancellationToken cancellationToken = default);
  Task<ApiResult<ServerAlertResponseDto>> UpdateServerAlert(ServerAlertRequestDto request, CancellationToken cancellationToken = default);
}
