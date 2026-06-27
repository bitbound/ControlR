using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using Microsoft.Extensions.Logging;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<Version>> IVersionApi.GetCurrentAgentVersion(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var version = await _client.GetFromJsonAsync<Version>($"{HttpConstants.VersionEndpoint}/agent", cancellationToken);
      _logger.LogInformation("Latest Agent version on server: {LatestAgentVersion}", version);
      return version;
    });
  }

  async Task<ApiResult<Version>> IVersionApi.GetCurrentServerVersion(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<Version>($"{HttpConstants.VersionEndpoint}/server", cancellationToken));
  }
}
