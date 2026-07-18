using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IDesktopSessionsApi
{
  [ApiRoute($"{HttpConstants.V0.DevicesEndpoint}/{{deviceId}}/desktop-sessions", "GET")]
  Task<ApiResult<List<V0Dtos.DesktopSessionResponseDto>>> GetActiveDesktopSessions(Guid deviceId, CancellationToken cancellationToken = default);
}