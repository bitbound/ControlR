using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<Version>> IServerVersionApi.GetCurrentServerVersion(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<Version>(HttpConstants.ServerVersionEndpoint, cancellationToken));
  }
}
