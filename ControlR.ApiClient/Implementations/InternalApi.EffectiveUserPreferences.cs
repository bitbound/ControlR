using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<EffectiveUserPreferencesDto>> IEffectiveUserPreferencesApi.GetEffectiveUserPreferences(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<EffectiveUserPreferencesDto>(HttpConstants.Internal.EffectiveUserPreferencesEndpoint, cancellationToken));
  }
}