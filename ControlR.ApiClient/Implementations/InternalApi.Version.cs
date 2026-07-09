using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using Microsoft.Extensions.Logging;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<Version>> IVersionApi.GetCurrentAgentVersion(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var version = await _client.HttpClient.GetFromJsonAsync<Version>($"{HttpConstants.Internal.VersionEndpoint}/agent", cancellationToken);
      _client.Logger.LogInformation("Latest Agent version on server: {LatestAgentVersion}", version);
      return version;
    });
  }

  async Task<ApiResult<Version>> IVersionApi.GetCurrentServerVersion(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<Version>($"{HttpConstants.Internal.VersionEndpoint}/server", cancellationToken));
  }

  async Task<ApiResult<string>> IVersionApi.GetReleaseNotes(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetStringAsync($"{HttpConstants.Internal.VersionEndpoint}/release-notes", cancellationToken));
  }
}
