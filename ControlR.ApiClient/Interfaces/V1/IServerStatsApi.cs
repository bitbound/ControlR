using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerStats;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IServerStatsApi
{
  [ApiRoute($"{HttpConstants.V1.ServerStatsEndpoint}", "GET")]
  Task<ApiResult<ServerStatsDto>> GetServerStats(CancellationToken cancellationToken = default);
}
