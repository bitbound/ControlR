using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IServerStatsApi
{
  [ApiRoute("GET", "/api/internal/server-stats")]
  Task<ApiResult<ServerStatsDto>> GetServerStats(CancellationToken cancellationToken = default);
}
