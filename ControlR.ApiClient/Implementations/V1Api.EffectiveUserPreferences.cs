using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectiveUserPreferences;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<EffectiveUserPreferencesDto>> IEffectiveUserPreferencesApi.GetEffectiveUserPreferences(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<EffectiveUserPreferencesDto>(
        $"{HttpConstants.V1.EffectiveUserPreferencesEndpoint}?tenantId={tenantId}",
        cancellationToken));
  }
}
