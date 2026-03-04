using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using Microsoft.Extensions.Logging;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<Version>> IAgentVersionApi.GetCurrentAgentVersion(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var version = await _client.GetFromJsonAsync<Version>(HttpConstants.AgentVersionEndpoint, cancellationToken);
      _logger.LogInformation("Latest Agent version on server: {LatestAgentVersion}", version);
      return version;
    });
  }
}
