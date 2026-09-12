using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectiveUserPreferences;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IEffectiveUserPreferencesApi
{
  [ApiRoute($"{HttpConstants.V1.EffectiveUserPreferencesEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<EffectiveUserPreferencesDto>> GetEffectiveUserPreferences(Guid tenantId, CancellationToken cancellationToken = default);
}
