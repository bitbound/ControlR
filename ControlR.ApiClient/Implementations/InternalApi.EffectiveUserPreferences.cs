using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<EffectiveUserPreferencesDto>> IEffectiveUserPreferencesApi.GetEffectiveUserPreferences(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<EffectiveUserPreferencesDto>(HttpConstants.Internal.EffectiveUserPreferencesEndpoint, cancellationToken));
  }
}