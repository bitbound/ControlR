using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IServerStatsApi
{
  Task<ApiResult<ServerStatsDto>> GetServerStats(CancellationToken cancellationToken = default);
}
