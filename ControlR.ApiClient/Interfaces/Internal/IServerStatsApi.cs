using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IServerStatsApi
{
  [ApiRoute($"{HttpConstants.Internal.ServerStatsEndpoint}", "GET")]
  Task<ApiResult<ServerStatsDto>> GetServerStats(CancellationToken cancellationToken = default);
}
