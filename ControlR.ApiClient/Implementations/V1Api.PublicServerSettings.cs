using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PublicServerSettings;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<PublicServerSettingsDto>> IPublicServerSettingsApi.GetPublicServerSettings(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PublicServerSettingsDto>(
        HttpConstants.V1.PublicServerSettingsEndpoint,
        cancellationToken));
  }
}
