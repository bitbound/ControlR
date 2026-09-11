using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using EffectiveDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectiveUserPreferences;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IEffectiveUserPreferencesApi
{
  [ApiRoute($"{HttpConstants.V1.EffectiveUserPreferencesEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<EffectiveDtos.EffectiveUserPreferencesDto>> GetEffectiveUserPreferences(Guid tenantId, CancellationToken cancellationToken = default);
}
