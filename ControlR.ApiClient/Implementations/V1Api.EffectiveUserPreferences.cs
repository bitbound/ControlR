using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using EffectiveDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectiveUserPreferences;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<EffectiveDtos.EffectiveUserPreferencesDto>> IEffectiveUserPreferencesApi.GetEffectiveUserPreferences(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<EffectiveDtos.EffectiveUserPreferencesDto>(
        $"{HttpConstants.V1.EffectiveUserPreferencesEndpoint}?tenantId={tenantId}",
        cancellationToken));
  }
}
