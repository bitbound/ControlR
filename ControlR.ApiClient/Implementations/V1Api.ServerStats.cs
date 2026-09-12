using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using StatsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerStats;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<StatsDtos.ServerStatsDto>> IServerStatsApi.GetServerStats(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<StatsDtos.ServerStatsDto>(
        HttpConstants.V1.ServerStatsEndpoint,
        cancellationToken));
  }
}
